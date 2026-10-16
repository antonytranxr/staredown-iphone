#import <ARKit/ARKit.h>
#import <CoreImage/CoreImage.h>
#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

// ARSession is the sole front-camera owner. No simultaneous WebCamTexture or
// AVCaptureSession is opened; its captured image also supplies the live head.
@interface StaredownMobileCameraBridge : NSObject <ARSessionDelegate>
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
@property(nonatomic) UIInterfaceOrientation orientation;
@end

@implementation StaredownMobileCameraBridge
- (void)session:(ARSession *)session didUpdateFrame:(ARFrame *)frame {
    @autoreleasepool {
    CIContext *context;
    UIInterfaceOrientation orientation;
    @synchronized(self) {
        if (!self.running || session != self.session) return;
        ARFaceAnchor *face = nil;
        NSUInteger count = 0;
        for (ARAnchor *anchor in frame.anchors) if ([anchor isKindOfClass:[ARFaceAnchor class]]) {
            count++; face = (ARFaceAnchor *)anchor;
        }
        BOOL valid = count == 1 && face.isTracked;
        float left = 0, right = 0, yaw = 0, pitch = 0;
        if (valid) {
            NSNumber *l = face.blendShapes[ARBlendShapeLocationEyeBlinkLeft];
            NSNumber *r = face.blendShapes[ARBlendShapeLocationEyeBlinkRight];
            simd_float4x4 relative = simd_mul(simd_inverse(frame.camera.transform), face.transform);
            yaw = atan2f(relative.columns[2].x, relative.columns[2].z) * 180.0f / M_PI;
            pitch = asinf(fmaxf(-1, fminf(1, -relative.columns[2].y))) * 180.0f / M_PI;
            valid = l != nil && r != nil && fabsf(yaw) <= 18 && fabsf(pitch) <= 25;
            if (valid) { left = l.floatValue; right = r.floatValue; }
        }
        self.left = left; self.right = right; self.yaw = yaw; self.pitch = pitch;
        self.valid = valid; self.timestamp = frame.timestamp;
        self.updatedAt = CACurrentMediaTime(); self.sequence++;
        if (frame.timestamp < self.nextThumbnail) return;
        // Face samples remain at the selected capture cadence; only the tiny
        // social video feed slows down when iOS reports thermal pressure.
        double interval = NSProcessInfo.processInfo.thermalState >= NSProcessInfoThermalStateSerious ? .25 : 1.0 / 6.0;
        self.nextThumbnail = frame.timestamp + interval;
        context = self.context; orientation = self.orientation;
    }

        CIImage *image = [CIImage imageWithCVPixelBuffer:frame.capturedImage];
        CGImagePropertyOrientation exif = kCGImagePropertyOrientationUp;
        if (orientation == UIInterfaceOrientationPortrait) exif = kCGImagePropertyOrientationRight;
        else if (orientation == UIInterfaceOrientationPortraitUpsideDown) exif = kCGImagePropertyOrientationLeft;
        else if (orientation == UIInterfaceOrientationLandscapeLeft) exif = kCGImagePropertyOrientationDown;
        image = [image imageByApplyingOrientation:exif];
        CGRect extent = image.extent;
        CGFloat side = MIN(extent.size.width, extent.size.height);
        CGRect crop = CGRectMake(CGRectGetMidX(extent) - side / 2, CGRectGetMidY(extent) - side / 2, side, side);
        image = [image imageByCroppingToRect:crop];
        image = [image imageByApplyingTransform:CGAffineTransformMakeTranslation(-crop.origin.x, -crop.origin.y)];
        image = [image imageByApplyingTransform:CGAffineTransformMakeScale(160.0 / side, 160.0 / side)];
        image = [image imageByApplyingTransform:CGAffineTransformMake(-1, 0, 0, 1, 160, 0)];
        // GPU image processing and JPEG encoding do not hold the lock used by
        // Unity's sample poll. A slow thumbnail cannot stall game/eye updates.
        CGImageRef cg = [context createCGImage:image fromRect:CGRectMake(0, 0, 160, 160)];
        if (!cg) return;
        UIImage *ui = [UIImage imageWithCGImage:cg];
        CGImageRelease(cg);
        NSData *encoded = nil;
        for (CGFloat quality = .5; quality >= .09; quality -= .1) {
            encoded = UIImageJPEGRepresentation(ui, quality);
            if (encoded.length > 0 && encoded.length <= 5000) break;
        }
        @synchronized(self) {
            if (self.running && session == self.session && encoded.length > 0 && encoded.length <= 5000) {
                self.jpeg = encoded; self.frameSequence++; self.thumbnailAt = CACurrentMediaTime();
            }
        }
    }
}
- (void)session:(ARSession *)session didFailWithError:(NSError *)error {
    @synchronized(self) { if (session == self.session) self.valid = NO; }
}
- (void)sessionWasInterrupted:(ARSession *)session {
    @synchronized(self) { if (session == self.session) self.valid = NO; }
}
@end

static StaredownMobileCameraBridge *bridge;
extern "C" {
bool StareMobileIOSSupported() { return ARFaceTrackingConfiguration.isSupported; }
bool StareMobileIOSStart() {
    if (!ARFaceTrackingConfiguration.isSupported) return false;
    if (!bridge) bridge = [StaredownMobileCameraBridge new];
    @synchronized(bridge) {
        [bridge.session pause];
        bridge.running = YES; bridge.valid = NO; bridge.jpeg = nil;
        bridge.orientation = UIInterfaceOrientationLandscapeLeft;
        bridge.sequence = bridge.frameSequence = 0; bridge.timestamp = bridge.updatedAt = bridge.nextThumbnail = bridge.thumbnailAt = 0;
        bridge.context = [CIContext contextWithOptions:@{kCIContextUseSoftwareRenderer:@NO, kCIContextCacheIntermediates:@NO}];
        bridge.session = [ARSession new]; bridge.session.delegate = bridge;
        bridge.session.delegateQueue = dispatch_queue_create("com.antony.staredown.facecamera", DISPATCH_QUEUE_SERIAL);
        ARFaceTrackingConfiguration *configuration = [ARFaceTrackingConfiguration new];
        configuration.maximumNumberOfTrackedFaces = MIN(2, ARFaceTrackingConfiguration.supportedNumberOfTrackedFaces);
        configuration.lightEstimationEnabled = NO;
        configuration.providesAudioData = NO; // Vivox is the sole microphone owner.
        // ARKit otherwise commonly selects a 60 Hz high-resolution feed. Eye
        // classification needs 30 Hz; the transmitted head is only 160x160.
        // Choose an advertised 30 Hz format with the fewest source pixels.
        ARVideoFormat *chosen = nil;
        for (ARVideoFormat *format in ARFaceTrackingConfiguration.supportedVideoFormats) {
            if (format.framesPerSecond != 30) continue;
            if (!chosen || format.imageResolution.width * format.imageResolution.height < chosen.imageResolution.width * chosen.imageResolution.height)
                chosen = format;
        }
        if (chosen) configuration.videoFormat = chosen;
        [bridge.session runWithConfiguration:configuration options:ARSessionRunOptionResetTracking | ARSessionRunOptionRemoveExistingAnchors];
    }
    // UIKit scene state is read only on the platform main thread, never by the
    // background ARSession analyzer. Landscape remains fixed during a session.
    dispatch_async(dispatch_get_main_queue(), ^{
        UIInterfaceOrientation orientation = UIInterfaceOrientationLandscapeLeft;
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if ([scene isKindOfClass:UIWindowScene.class] && scene.activationState == UISceneActivationStateForegroundActive) {
                orientation = ((UIWindowScene *)scene).interfaceOrientation; break;
            }
        }
        @synchronized(bridge) { bridge.orientation = orientation; }
    });
    return true;
}
void StareMobileIOSStop() {
    if (!bridge) return;
    @synchronized(bridge) {
        bridge.running = bridge.valid = NO; [bridge.session pause];
        bridge.session.delegate = nil; bridge.session = nil; bridge.jpeg = nil; bridge.context = nil;
    }
}
int StareMobileIOSThermalLevel() { return (int)NSProcessInfo.processInfo.thermalState; }
const char *StareMobileIOSPollAfter(long long previousSequence) {
    if (!bridge) return NULL;
    @synchronized(bridge) {
        if (!bridge.running || bridge.sequence == 0 || bridge.sequence <= previousSequence) return NULL;
        double age = fmax(0, CACurrentMediaTime() - bridge.updatedAt);
        NSString *json = [NSString stringWithFormat:
            @"{\"sequence\":%lld,\"timestamp\":%.6f,\"age\":%.6f,\"valid\":%s,\"left\":%.5f,\"right\":%.5f,\"yaw\":%.3f,\"pitch\":%.3f}",
            bridge.sequence, bridge.timestamp, age, bridge.valid ? "true" : "false", bridge.left, bridge.right, bridge.yaw, bridge.pitch];
        return strdup(json.UTF8String);
    }
}
const char *StareMobileIOSPoll() { return StareMobileIOSPollAfter(-1); }
long long StareMobileIOSFrameSequence() { if (!bridge) return 0; @synchronized(bridge) { return bridge.frameSequence; } }
void *StareMobileIOSCopyFrame(int *length) {
    *length = 0; if (!bridge) return NULL;
    @synchronized(bridge) {
        if (!bridge.running || bridge.jpeg.length == 0 || bridge.jpeg.length > 5000 || CACurrentMediaTime() - bridge.thumbnailAt > .6) return NULL;
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
