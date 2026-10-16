// Pure route policy is compiled independently by the native regression harness.
// Do not change the audio category, mode, preferred input, or session activation:
// Vivox owns recording/AEC and Unity owns its playback engine.
static bool StareReceiverNeedsSpeaker(bool observing, bool interrupted, bool foreground,
                                     bool playAndRecord, int outputCount,
                                     bool everyOutputReceiver, bool externalInput) {
    return observing && !interrupted && foreground && playAndRecord &&
           outputCount > 0 && everyOutputReceiver && !externalInput;
}

// A temporary override is invalidated by any subsequent route change. Only
// the narrowly matched notification produced by our own correction retains it.
static bool StareKeepSpeakerOverrideOwnership(bool owned, bool awaitingOwnNotification,
        bool overrideReason, bool previousReceiverOnly, bool currentSpeakerOnly,
        bool externalInput, bool recentOwnRequest) {
    return owned && awaitingOwnNotification && overrideReason && previousReceiverOnly &&
           currentSpeakerOnly && !externalInput && recentOwnRequest;
}

static bool StareReleaseOwnedSpeakerOverride(bool owned, bool playAndRecord,
        bool speakerOnly, bool externalInput) {
    return owned && playAndRecord && speakerOnly && !externalInput;
}

#ifndef STARE_AUDIO_ROUTE_POLICY_ONLY
#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

@interface StaredownAudioRouteBridge : NSObject
@property(nonatomic) BOOL observing, interrupted, scheduled, appliedSpeakerOverride, awaitingOwnOverrideNotification;
@property(nonatomic) NSTimeInterval ownOverrideAt;
@property(nonatomic) NSUInteger generation;
@property(nonatomic,copy) NSString *lastRouteSignature;
- (void)start;
- (void)stop;
- (void)schedule;
@end

@implementation StaredownAudioRouteBridge
- (BOOL)hasExternalInput:(AVAudioSessionRouteDescription *)route {
    // An output override also selects the built-in microphone. Leave USB,
    // headset and Bluetooth microphones alone, even if output is the receiver.
    for (AVAudioSessionPortDescription *input in route.inputs)
        if (![input.portType isEqualToString:AVAudioSessionPortBuiltInMic]) return YES;
    return NO;
}
- (BOOL)allOutputs:(AVAudioSessionRouteDescription *)route haveType:(AVAudioSessionPort)type {
    if (route.outputs.count == 0) return NO;
    for (AVAudioSessionPortDescription *output in route.outputs)
        if (![output.portType isEqualToString:type]) return NO;
    return YES;
}
- (void)repairReceiver {
    AVAudioSession *session = AVAudioSession.sharedInstance;
    AVAudioSessionRouteDescription *route = session.currentRoute;
    NSMutableArray<NSString *> *inputs = [NSMutableArray new], *outputs = [NSMutableArray new];
    for (AVAudioSessionPortDescription *port in route.inputs) [inputs addObject:port.portType];
    for (AVAudioSessionPortDescription *port in route.outputs) [outputs addObject:port.portType];
    NSString *signature = [NSString stringWithFormat:@"category=%@ mode=%@ input=%@ output=%@",
        session.category, session.mode, [inputs componentsJoinedByString:@","], [outputs componentsJoinedByString:@","]];
    // Log only changes in category/mode/port types, never names, UIDs or device
    // addresses. Repeated focus/recovery requests cannot spam identical routes.
    if (![signature isEqualToString:self.lastRouteSignature]) {
        self.lastRouteSignature = signature; NSLog(@"[STARE AUDIO ROUTE] %@", signature);
    }
    BOOL playAndRecord = [session.category isEqualToString:AVAudioSessionCategoryPlayAndRecord];
    BOOL externalInput = [self hasExternalInput:route];
    BOOL receiverOnly = [self allOutputs:route haveType:AVAudioSessionPortBuiltInReceiver];
    if (!StareReceiverNeedsSpeaker(self.observing, self.interrupted,
            UIApplication.sharedApplication.applicationState == UIApplicationStateActive,
            playAndRecord, (int)route.outputs.count, receiverOnly, externalInput)) return;
    NSError *error = nil;
    // Mark before the call: AVAudioSession may post its route notification
    // synchronously. Failure revokes the provisional ownership below.
    self.appliedSpeakerOverride = self.awaitingOwnOverrideNotification = YES;
    self.ownOverrideAt = NSProcessInfo.processInfo.systemUptime;
    if ([session overrideOutputAudioPort:AVAudioSessionPortOverrideSpeaker error:&error]) {
        NSLog(@"[STARE AUDIO ROUTE] repaired built-in receiver to speaker / category=%@ mode=%@", session.category, session.mode);
    } else {
        self.appliedSpeakerOverride = self.awaitingOwnOverrideNotification = NO;
        NSLog(@"[STARE AUDIO ROUTE] speaker repair deferred / error=%ld", (long)error.code);
    }
}
- (void)schedule {
    if (!NSThread.isMainThread) {
        dispatch_async(dispatch_get_main_queue(), ^{ [self schedule]; }); return;
    }
    if (!self.observing || self.scheduled) return;
    self.scheduled = YES;
    NSUInteger generation = self.generation;
    // Let Vivox / the OS complete the route transition before examining it.
    // The correction's own notification sees Speaker and does nothing.
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(.12 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (!self.observing || generation != self.generation) return;
        self.scheduled = NO; [self repairReceiver];
    });
}
- (void)routeChanged:(NSNotification *)notification {
    // The system drops a temporary override when the route changes. Revoke
    // ownership for headphones connecting/disconnecting, category changes,
    // other callers' overrides, and every other route transition. Keep it only
    // for our own immediate Receiver -> Speaker notification.
    NSUInteger reason = [notification.userInfo[AVAudioSessionRouteChangeReasonKey] unsignedIntegerValue];
    AVAudioSessionRouteDescription *previous = notification.userInfo[AVAudioSessionRouteChangePreviousRouteKey];
    void (^handle)(void) = ^{
        if (!self.observing) return;
        AVAudioSessionRouteDescription *current = AVAudioSession.sharedInstance.currentRoute;
        self.appliedSpeakerOverride = StareKeepSpeakerOverrideOwnership(self.appliedSpeakerOverride,
            self.awaitingOwnOverrideNotification, reason == AVAudioSessionRouteChangeReasonOverride,
            [self allOutputs:previous haveType:AVAudioSessionPortBuiltInReceiver],
            [self allOutputs:current haveType:AVAudioSessionPortBuiltInSpeaker],
            [self hasExternalInput:current] || [self hasExternalInput:previous],
            NSProcessInfo.processInfo.systemUptime - self.ownOverrideAt <= .5);
        self.awaitingOwnOverrideNotification = NO;
        [self schedule];
    };
    if (NSThread.isMainThread) handle(); else dispatch_async(dispatch_get_main_queue(), handle);
}
- (void)interruptionChanged:(NSNotification *)notification {
    NSUInteger type = [notification.userInfo[AVAudioSessionInterruptionTypeKey] unsignedIntegerValue];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!self.observing) return;
        self.interrupted = type == AVAudioSessionInterruptionTypeBegan;
        if (self.interrupted) self.appliedSpeakerOverride = self.awaitingOwnOverrideNotification = NO;
        else [self schedule];
    });
}
- (void)becameActive:(NSNotification *)notification { [self schedule]; }
- (void)mediaReset:(NSNotification *)notification {
    dispatch_async(dispatch_get_main_queue(), ^{
        self.appliedSpeakerOverride = self.awaitingOwnOverrideNotification = NO; [self schedule];
    });
}
- (void)start {
    if (self.observing) return;
    self.observing = YES; self.interrupted = NO; self.generation++;
    NSNotificationCenter *center = NSNotificationCenter.defaultCenter;
    [center addObserver:self selector:@selector(routeChanged:) name:AVAudioSessionRouteChangeNotification object:nil];
    [center addObserver:self selector:@selector(interruptionChanged:) name:AVAudioSessionInterruptionNotification object:nil];
    [center addObserver:self selector:@selector(mediaReset:) name:AVAudioSessionMediaServicesWereResetNotification object:nil];
    [center addObserver:self selector:@selector(becameActive:) name:UIApplicationDidBecomeActiveNotification object:nil];
    [self schedule];
}
- (void)stop {
    self.observing = NO; self.scheduled = NO; self.generation++;
    [NSNotificationCenter.defaultCenter removeObserver:self];
    AVAudioSession *session = AVAudioSession.sharedInstance;
    AVAudioSessionRouteDescription *route = session.currentRoute;
    // Release only our own temporary override. Never clear or select an
    // external route, and never deactivate Vivox's shared audio session.
    if (StareReleaseOwnedSpeakerOverride(self.appliedSpeakerOverride,
            [session.category isEqualToString:AVAudioSessionCategoryPlayAndRecord],
            [self allOutputs:route haveType:AVAudioSessionPortBuiltInSpeaker], [self hasExternalInput:route]))
        [session overrideOutputAudioPort:AVAudioSessionPortOverrideNone error:nil];
    self.appliedSpeakerOverride = self.awaitingOwnOverrideNotification = NO;
}
@end

static StaredownAudioRouteBridge *stareAudioRoute;
static void StareAudioOnMain(dispatch_block_t action) {
    if (NSThread.isMainThread) action(); else dispatch_async(dispatch_get_main_queue(), action);
}
extern "C" {
void StareMobileIOSAudioRouteBegin() {
    StareAudioOnMain(^{ if (!stareAudioRoute) stareAudioRoute = [StaredownAudioRouteBridge new]; [stareAudioRoute start]; });
}
void StareMobileIOSAudioRouteRefresh() { StareAudioOnMain(^{ [stareAudioRoute schedule]; }); }
void StareMobileIOSAudioRouteEnd() {
    StareAudioOnMain(^{ [stareAudioRoute stop]; stareAudioRoute = nil; });
}
}
#endif
