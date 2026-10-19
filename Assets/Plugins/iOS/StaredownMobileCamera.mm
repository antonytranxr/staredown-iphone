#include <stdlib.h>
#include <string.h>
#include <math.h>

// These exact projection helpers also run in the native regression harness.
// ARKit's face +Z points out of the face; comparing it with the actual vector
// toward the camera avoids assuming the front camera uses the rear-camera basis.
struct StareCameraVector { double x, y, z; };
struct StareCameraAffine { double a, b, c, d, tx, ty; };
static double StareCameraDot(StareCameraVector a, StareCameraVector b) { return a.x*b.x+a.y*b.y+a.z*b.z; }
static bool StareCameraNormalize(StareCameraVector *value) {
    double length = sqrt(StareCameraDot(*value, *value));
    if (!isfinite(length) || length < .000001) return false;
    value->x /= length; value->y /= length; value->z /= length; return true;
}
static StareCameraVector StareCameraReject(StareCameraVector value, StareCameraVector axis) {
    double amount = StareCameraDot(value, axis);
    return {value.x-axis.x*amount, value.y-axis.y*amount, value.z-axis.z*amount};
}
static bool StareMobileFaceAngles(StareCameraVector outward, StareCameraVector towardCamera,
                                 StareCameraVector screenRight, StareCameraVector screenUp,
                                 double *yaw, double *pitch) {
    *yaw = *pitch = 0;
    if (!StareCameraNormalize(&outward) || !StareCameraNormalize(&towardCamera)) return false;
    screenRight = StareCameraReject(screenRight, towardCamera);
    if (!StareCameraNormalize(&screenRight)) return false;
    screenUp = StareCameraReject(StareCameraReject(screenUp, towardCamera), screenRight);
    if (!StareCameraNormalize(&screenUp)) return false;
    double right = StareCameraDot(outward, screenRight), up = StareCameraDot(outward, screenUp);
    double front = StareCameraDot(outward, towardCamera);
    *yaw = atan2(right, front) * 180.0 / M_PI;
    *pitch = atan2(up, sqrt(right*right+front*front)) * 180.0 / M_PI;
    return isfinite(*yaw) && isfinite(*pitch);
}
static bool StareMobileFaceFacing(double yaw, double pitch) {
    return isfinite(yaw) && isfinite(pitch) && fabs(yaw) <= 18 && fabs(pitch) <= 25;
}
static StareCameraAffine StareCameraThen(StareCameraAffine first, StareCameraAffine second) {
    return {second.a*first.a+second.c*first.b, second.b*first.a+second.d*first.b,
            second.a*first.c+second.c*first.d, second.b*first.c+second.d*first.d,
            second.a*first.tx+second.c*first.ty+second.tx, second.b*first.tx+second.d*first.ty+second.ty};
}
static bool StareMobileImageTransform(StareCameraAffine display, double width, double height,
                                      double size, StareCameraAffine *result) {
    double determinant = display.a*display.d-display.b*display.c;
    if (!isfinite(determinant) || !isfinite(display.tx) || !isfinite(display.ty) || fabs(determinant) < .000001 ||
        !isfinite(width) || !isfinite(height) || !isfinite(size) || width <= 0 || height <= 0 || size <= 0) return false;
    // ARFrame displayTransform consumes normalized top-left image coordinates.
    // Core Image uses bottom-left pixel coordinates. Convert both ends rather
    // than applying a hand-written EXIF table with another front-camera flip.
    StareCameraAffine rawToTop = {1/width, 0, 0, -1/height, 0, 1};
    StareCameraAffine canonical = determinant < 0 ? StareCameraAffine{-1,0,0,1,1,0} : StareCameraAffine{1,0,0,1,0,0};
    StareCameraAffine topToOutput = {size, 0, 0, -size, 0, size};
    *result = StareCameraThen(StareCameraThen(StareCameraThen(rawToTop, display), canonical), topToOutput);
    return true;
}
static bool StareMobileFrameCanCommit(bool running, bool sameSession, unsigned long selectedOrientation,
                                     unsigned long currentOrientation, double capturedAt, double now) {
    double age = now-capturedAt;
    return running && sameSession && selectedOrientation == currentOrientation &&
        isfinite(age) && age >= 0 && age <= .6;
}
static double StareMobileCaptureObservedAt(double callbackAt,double captureTimestamp,double latestCaptureTimestamp) {
    // ARFrame timestamps share ARKit's clock. Subtract their difference from
    // the local monotonic clock so a delayed callback cannot freshen a capture,
    // without assuming ARKit and Quartz use the same absolute clock origin.
    if(!isfinite(callbackAt)||!isfinite(captureTimestamp)||!isfinite(latestCaptureTimestamp)) return NAN;
    return callbackAt-fmax(0,latestCaptureTimestamp-captureTimestamp);
}
struct StareMobileEyeSample {
    long long sequence; double timestamp, observedAt;
    bool valid; int reason; float left, right, yaw, pitch;
};
struct StareMobileEyeRing {
    static const int Capacity = 256;
    StareMobileEyeSample values[Capacity]; int head = 0, count = 0;
    long long dropped = 0, pendingDropped = 0;
    void clear() { head = count = 0; pendingDropped = 0; }
    void reset() { clear(); dropped = 0; }
    void push(StareMobileEyeSample sample) {
        if(count == Capacity) { head = (head+1)%Capacity; count--; dropped++; pendingDropped++; }
        values[(head+count)%Capacity] = sample; count++;
    }
    int drain(StareMobileEyeSample *output, long long *lost) {
        int amount = count; *lost = pendingDropped;
        for(int i=0;i<amount;i++) output[i] = values[(head+i)%Capacity];
        clear(); return amount;
    }
};

#ifndef STARE_MOBILE_CAMERA_POLICY_ONLY
#import <ARKit/ARKit.h>
#import <CoreImage/CoreImage.h>
#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>

// ARSession is the sole front-camera owner. No simultaneous WebCamTexture or
// AVCaptureSession is opened; its captured image also supplies the live head.
@interface StaredownMobileCameraBridge : NSObject <ARSessionDelegate> {
@public StareMobileEyeRing eyeSamples;
}
@property(nonatomic,strong) ARSession *session;
@property(nonatomic,strong) CIContext *context;
@property(nonatomic,strong) NSData *jpeg;
@property(nonatomic) BOOL running;
@property(nonatomic) long long sequence;
@property(nonatomic) long long frameSequence;
@property(nonatomic) double timestamp;
@property(nonatomic) double updatedAt;
@property(nonatomic) double thumbnailAt;
@property(nonatomic) double nextThumbnail;
@property(nonatomic) float left, right, yaw, pitch;
@property(nonatomic) BOOL valid;
@property(nonatomic) int reason;
@property(nonatomic) UIInterfaceOrientation orientation;
@property(nonatomic) NSUInteger orientationGeneration;
@property(nonatomic) NSUInteger thumbnailGeneration;
@property(nonatomic,strong) dispatch_queue_t encodeQueue;
@property(nonatomic) BOOL encodeBusy;
@property(nonatomic) long long encodeSkips, captureGaps;
@property(nonatomic) double nativeFPS, encodeMilliseconds;
@property(nonatomic) NSInteger formatFPS;
@end

@implementation StaredownMobileCameraBridge
- (void)session:(ARSession *)session didUpdateFrame:(ARFrame *)frame {
    @autoreleasepool {
        CIContext *context;
        UIInterfaceOrientation orientation;
        NSUInteger orientationGeneration;
        double capturedAt = StareMobileCaptureObservedAt(CACurrentMediaTime(),frame.timestamp,session.currentFrame.timestamp);
        @synchronized(self) {
            if (!self.running || session != self.session) return;
            orientation = self.orientation;
            ARFaceAnchor *face = nil; NSUInteger count = 0;
            for (ARAnchor *anchor in frame.anchors)
                if ([anchor isKindOfClass:[ARFaceAnchor class]] && ((ARFaceAnchor *)anchor).isTracked) {
                    count++; face = (ARFaceAnchor *)anchor;
                }
            BOOL valid = count == 1; int reason = valid ? 0 : count > 1 ? 2 : 1;
            float left = 0, right = 0, yaw = 0, pitch = 0;
            if (valid) {
                NSNumber *l = face.blendShapes[ARBlendShapeLocationEyeBlinkLeft];
                NSNumber *r = face.blendShapes[ARBlendShapeLocationEyeBlinkRight];
                simd_float4x4 screenCamera = simd_inverse([frame.camera viewMatrixForOrientation:orientation]);
                simd_float3 direction = frame.camera.transform.columns[3].xyz-face.transform.columns[3].xyz;
                simd_float3 normal = face.transform.columns[2].xyz, rightAxis = screenCamera.columns[0].xyz, upAxis = screenCamera.columns[1].xyz;
                double y, p;
                bool pose = StareMobileFaceAngles({normal.x,normal.y,normal.z}, {direction.x,direction.y,direction.z},
                    {rightAxis.x,rightAxis.y,rightAxis.z}, {upAxis.x,upAxis.y,upAxis.z}, &y, &p);
                yaw = y; pitch = p;
                valid = pose && l != nil && r != nil && isfinite(l.floatValue) && isfinite(r.floatValue);
                if (!valid) reason = 3;
                else if (!StareMobileFaceFacing(yaw,pitch)) { valid = NO; reason = 4; }
                if (valid) { left = l.floatValue; right = r.floatValue; }
            }
            double interval = frame.timestamp-self.timestamp;
            if(self.sequence > 0 && interval > 0) {
                double measured = 1.0/interval;
                self.nativeFPS = self.nativeFPS > 0 ? self.nativeFPS*.9+measured*.1 : measured;
                if(interval > .12) self.captureGaps++;
            }
            self.left = left; self.right = right; self.yaw = yaw; self.pitch = pitch;
            self.valid = valid; self.reason = reason; self.timestamp = frame.timestamp;
            self.updatedAt = capturedAt; self.sequence++;
            eyeSamples.push({self.sequence,frame.timestamp,capturedAt,(bool)valid,reason,left,right,yaw,pitch});
            if(frame.timestamp < self.nextThumbnail) return;
            if(self.encodeBusy) { self.encodeSkips++; return; }
            double videoInterval = NSProcessInfo.processInfo.thermalState >= NSProcessInfoThermalStateSerious ? .25 : 1.0/6.0;
            self.nextThumbnail = frame.timestamp+videoInterval;
            self.encodeBusy = YES; context = self.context; orientationGeneration = self.orientationGeneration;
        }
        // Only the pixel buffer, affine and context cross to the worker. Never
        // retain ARFrame: its camera/depth buffers belong to ARKit's frame pool.
        CVPixelBufferRef pixels = CVPixelBufferRetain(frame.capturedImage);
        CGAffineTransform display = [frame displayTransformForOrientation:orientation viewportSize:CGSizeMake(160,160)];
        dispatch_async(self.encodeQueue, ^{
            @autoreleasepool {
                double started = CACurrentMediaTime(); NSData *encoded = nil;
                CIImage *image = [CIImage imageWithCVPixelBuffer:pixels]; StareCameraAffine transform;
                if(StareMobileImageTransform({display.a,display.b,display.c,display.d,display.tx,display.ty}, image.extent.size.width, image.extent.size.height, 160, &transform)) {
                    image = [image imageByApplyingTransform:CGAffineTransformMake(transform.a,transform.b,transform.c,transform.d,transform.tx,transform.ty)];
                    image = [image imageByCroppingToRect:CGRectMake(0,0,160,160)];
                    CGImageRef cg = [context createCGImage:image fromRect:CGRectMake(0,0,160,160)];
                    if(cg) {
                        UIImage *ui = [UIImage imageWithCGImage:cg]; CGImageRelease(cg);
                        for(CGFloat quality=.5;quality>=.09;quality-=.1) {
                            encoded = UIImageJPEGRepresentation(ui,quality);
                            if(encoded.length > 0 && encoded.length <= 5000) break;
                        }
                    }
                }
                @synchronized(self) {
                    self.encodeMilliseconds = (CACurrentMediaTime()-started)*1000;
                    if(StareMobileFrameCanCommit(self.running,session == self.session,orientationGeneration,self.orientationGeneration,capturedAt,CACurrentMediaTime()) && encoded.length > 0 && encoded.length <= 5000) {
                        self.jpeg = encoded; self.frameSequence++; self.thumbnailAt = capturedAt; self.thumbnailGeneration = orientationGeneration;
                    }
                    // One shared worker survives camera restarts. An old job
                    // releases the slot before any new capture can occupy it.
                    self.encodeBusy = NO;
                }
                CVPixelBufferRelease(pixels);
            }
        });
    }
}
- (void)session:(ARSession *)session didFailWithError:(NSError *)error {
    @synchronized(self) { if (session == self.session) { self.valid = NO; self.reason = 5; } }
}
- (void)sessionWasInterrupted:(ARSession *)session {
    @synchronized(self) { if (session == self.session) { self.valid = NO; self.reason = 5; } }
}
@end

static StaredownMobileCameraBridge *bridge;
static UIInterfaceOrientation stareCameraOrientation = UIInterfaceOrientationPortrait;
static NSObject *StareCameraStateLock() {
    static NSObject *lock; static dispatch_once_t once;
    dispatch_once(&once, ^{ lock = [NSObject new]; }); return lock;
}
static void StareMobileReadOrientation() {
    UIInterfaceOrientation orientation;
    @synchronized(StareCameraStateLock()) { orientation = stareCameraOrientation; }
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if ([scene isKindOfClass:UIWindowScene.class] && scene.activationState == UISceneActivationStateForegroundActive) {
            UIInterfaceOrientation current = ((UIWindowScene *)scene).interfaceOrientation;
            if (current != UIInterfaceOrientationUnknown) orientation = current;
            break;
        }
    }
    @synchronized(StareCameraStateLock()) {
        stareCameraOrientation = orientation;
        if (bridge) @synchronized(bridge) {
            if (bridge.orientation != orientation) {
                bridge.orientation = orientation; bridge.orientationGeneration++; bridge.nextThumbnail = 0;
            }
        }
    }
}
extern "C" {
bool StareMobileIOSSupported() { return ARFaceTrackingConfiguration.isSupported; }
void StareMobileIOSRefreshOrientation() {
    if (NSThread.isMainThread) StareMobileReadOrientation();
    else dispatch_async(dispatch_get_main_queue(), ^{ StareMobileReadOrientation(); });
}
bool StareMobileIOSStart() {
    if (!ARFaceTrackingConfiguration.isSupported) return false;
    StareMobileIOSRefreshOrientation();
    @synchronized(StareCameraStateLock()) {
      if (!bridge) bridge = [StaredownMobileCameraBridge new];
      @synchronized(bridge) {
        [bridge.session pause];
        bridge.running = YES; bridge.valid = NO; bridge.reason = 1; bridge.jpeg = nil;
        bridge.orientation = stareCameraOrientation; bridge.orientationGeneration++;
        bridge.sequence = bridge.frameSequence = 0; bridge.timestamp = bridge.updatedAt = bridge.nextThumbnail = bridge.thumbnailAt = 0;
        bridge->eyeSamples.reset(); bridge.nativeFPS = bridge.encodeMilliseconds = 0;
        bridge.captureGaps = bridge.encodeSkips = 0;
        if(!bridge.encodeQueue) bridge.encodeQueue = dispatch_queue_create("com.antony.staredown.facejpeg",DISPATCH_QUEUE_SERIAL);
        bridge.context = [CIContext contextWithOptions:@{kCIContextUseSoftwareRenderer:@NO, kCIContextCacheIntermediates:@NO}];
        bridge.session = [ARSession new]; bridge.session.delegate = bridge;
        bridge.session.delegateQueue = dispatch_queue_create("com.antony.staredown.facecamera", DISPATCH_QUEUE_SERIAL);
        ARFaceTrackingConfiguration *configuration = [ARFaceTrackingConfiguration new];
        configuration.maximumNumberOfTrackedFaces = MIN(2, ARFaceTrackingConfiguration.supportedNumberOfTrackedFaces);
        configuration.lightEstimationEnabled = NO;
        configuration.providesAudioData = NO; // Vivox is the sole microphone owner.
        // Fast eye capture is independent of 30 Hz game rendering and 6 Hz
        // video. Prefer advertised 60 Hz at the fewest pixels; fall back to
        // supported 30 Hz rather than claiming an unsupported capture rate.
        ARVideoFormat *chosen = nil;
        for(NSNumber *rate in @[@60,@30]) {
            for (ARVideoFormat *format in ARFaceTrackingConfiguration.supportedVideoFormats) {
                if (format.framesPerSecond != rate.integerValue) continue;
                if (!chosen || format.imageResolution.width * format.imageResolution.height < chosen.imageResolution.width * chosen.imageResolution.height)
                    chosen = format;
            }
            if(chosen) break;
        }
        if (chosen) configuration.videoFormat = chosen;
        bridge.formatFPS = configuration.videoFormat.framesPerSecond;
        [bridge.session runWithConfiguration:configuration options:ARSessionRunOptionResetTracking | ARSessionRunOptionRemoveExistingAnchors];
      }
    }
    return true;
}
void StareMobileIOSStop() {
    if (!bridge) return;
    @synchronized(bridge) {
        bridge.running = bridge.valid = NO; [bridge.session pause];
        bridge.session.delegate = nil; bridge.session = nil; bridge.jpeg = nil; bridge.context = nil;
        bridge->eyeSamples.clear();
    }
}
int StareMobileIOSThermalLevel() { return (int)NSProcessInfo.processInfo.thermalState; }
void StareMobileIOSResetSamples() {
    if(!bridge) return; @synchronized(bridge) { bridge->eyeSamples.clear(); }
}
double StareMobileIOSLatestCaptureTimestamp() {
    if(!bridge) return 0; @synchronized(bridge) { return fmax(bridge.timestamp,bridge.session.currentFrame.timestamp); }
}
const char *StareMobileIOSDrainSamples() {
    if(!bridge) return NULL;
    StareMobileEyeSample samples[StareMobileEyeRing::Capacity]; int count;
    long long lost,total,nativeCount,frames,skips,gaps; double nativeFPS,encodeMs; NSInteger formatFPS;
    double now = CACurrentMediaTime();
    @synchronized(bridge) {
        if(!bridge.running || (bridge->eyeSamples.count == 0 && bridge->eyeSamples.pendingDropped == 0)) return NULL;
        count = bridge->eyeSamples.drain(samples,&lost); total = bridge->eyeSamples.dropped;
        nativeCount = bridge.sequence; frames = bridge.frameSequence; skips = bridge.encodeSkips; gaps = bridge.captureGaps;
        nativeFPS = bridge.nativeFPS; encodeMs = bridge.encodeMilliseconds; formatFPS = bridge.formatFPS;
    }
    NSMutableString *json = [NSMutableString stringWithFormat:
        @"{\"overflow\":%lld,\"dropped\":%lld,\"nativeSamples\":%lld,\"encodedFrames\":%lld,\"encodeSkips\":%lld,\"captureGaps\":%lld,\"nativeFPS\":%.2f,\"formatFPS\":%ld,\"encodeMs\":%.2f,\"samples\":[",
        lost,total,nativeCount,frames,skips,gaps,nativeFPS,(long)formatFPS,encodeMs];
    for(int i=0;i<count;i++) {
        auto sample=samples[i];
        [json appendFormat:@"%s{\"sequence\":%lld,\"timestamp\":%.6f,\"age\":%.6f,\"valid\":%s,\"reason\":%d,\"left\":%.5f,\"right\":%.5f,\"yaw\":%.3f,\"pitch\":%.3f}",
            i?",":"",sample.sequence,sample.timestamp,fmax(0,now-sample.observedAt),sample.valid?"true":"false",sample.reason,sample.left,sample.right,sample.yaw,sample.pitch];
    }
    [json appendString:@"]}"]; return strdup(json.UTF8String);
}
const char *StareMobileIOSPollAfter(long long previousSequence) {
    if (!bridge) return NULL;
    @synchronized(bridge) {
        if (!bridge.running || bridge.sequence == 0 || bridge.sequence <= previousSequence) return NULL;
        double age = fmax(0, CACurrentMediaTime() - bridge.updatedAt);
        NSString *json = [NSString stringWithFormat:
            @"{\"sequence\":%lld,\"timestamp\":%.6f,\"age\":%.6f,\"valid\":%s,\"reason\":%d,\"left\":%.5f,\"right\":%.5f,\"yaw\":%.3f,\"pitch\":%.3f}",
            bridge.sequence, bridge.timestamp, age, bridge.valid ? "true" : "false", bridge.reason, bridge.left, bridge.right, bridge.yaw, bridge.pitch];
        return strdup(json.UTF8String);
    }
}
const char *StareMobileIOSPoll() { return StareMobileIOSPollAfter(-1); }
long long StareMobileIOSFrameSequence() { if (!bridge) return 0; @synchronized(bridge) { return bridge.frameSequence; } }
void *StareMobileIOSCopyFrame(int *length) {
    *length = 0; if (!bridge) return NULL;
    @synchronized(bridge) {
        if (!StareMobileFrameCanCommit(bridge.running,true,bridge.thumbnailGeneration,bridge.orientationGeneration,bridge.thumbnailAt,CACurrentMediaTime()) || bridge.jpeg.length == 0 || bridge.jpeg.length > 5000) return NULL;
        *length = (int)bridge.jpeg.length;
        void *copy = malloc(*length); if (!copy) { *length = 0; return NULL; }
        memcpy(copy, bridge.jpeg.bytes, *length); return copy;
    }
}
void *StareMobileIOSCopyFrameAfter(long long previousSequence, long long *sequence, int *length) {
    *sequence = 0; *length = 0; if (!bridge) return NULL;
    @synchronized(bridge) {
        if (bridge.frameSequence <= previousSequence) return NULL;
        void *copy = StareMobileIOSCopyFrame(length);
        if (copy) *sequence = bridge.frameSequence;
        return copy;
    }
}
void StareMobileIOSFree(void *pointer) { free(pointer); }
}
#endif
