#import <Foundation/Foundation.h>
#import <dispatch/dispatch.h>

// Retain the bundle string for the lifetime of the app; C# copies its UTF-8 bytes.
extern "C" const char *StareInstalledBuildNumber()
{
    static NSString *build;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        build = [[[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"] copy];
    });
    return build.UTF8String;
}
