#define STARE_MOBILE_CAMERA_POLICY_ONLY
#include "../Assets/Plugins/iOS/StaredownMobileCamera.mm"
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>

static int checks;
static void check(bool condition,const char *message) {
    ++checks;if(!condition)throw std::runtime_error(message);
}
static void near(double value,double expected,const char *message) { check(fabs(value-expected)<.00001,message); }
struct Point { double x,y; };
static Point apply(Point p,StareCameraAffine a) { return {p.x*a.a+p.y*a.c+a.tx,p.x*a.b+p.y*a.d+a.ty}; }
static Point rotate(Point p,int quarter) {
    if(quarter==1)return {1-p.y,p.x};if(quarter==2)return {1-p.x,1-p.y};if(quarter==3)return {p.y,1-p.x};return p;
}
static StareCameraVector add(StareCameraVector a,StareCameraVector b,double scaleA,double scaleB) {
    return {a.x*scaleA+b.x*scaleB,a.y*scaleA+b.y*scaleB,a.z*scaleA+b.z*scaleB};
}
int main(int argc,char **argv) {
    std::ostringstream fixtures;fixtures<<"{\"fixtures\":[";bool first=true;
    const char *names[]={"LandscapeRight","Portrait","LandscapeLeft","PortraitUpsideDown"};
    for(int quarter=0;quarter<4;quarter++) {
        double roll=quarter*M_PI/2;
        StareCameraVector right={cos(roll),sin(roll),0},up={-sin(roll),cos(roll),0};
        for(int sign=-1;sign<=1;sign+=2) {
            StareCameraVector toward={0,0,(double)sign};
            for(int pose=0;pose<4;pose++) {
                double wantYaw=pose==1?12:pose==2?35:pose==3?180:0,wantPitch=pose==1?8:0;
                double y=wantYaw*M_PI/180,p=wantPitch*M_PI/180;
                auto normal=add(add(toward,right,cos(y),sin(y)),up,cos(p),sin(p));
                double yaw,pitch;bool got=StareMobileFaceAngles(normal,toward,right,up,&yaw,&pitch);
                check(got,"valid pose basis");near(fabs(yaw),wantYaw,"orientation-aware yaw matches face pose");near(pitch,wantPitch,"orientation-aware pitch matches face pose");
                bool valid=StareMobileFaceFacing(yaw,pitch);check(valid==(pose<2),"frontal/tilted accepted; turned/back-facing rejected");
                if(!first)fixtures<<",";first=false;
                fixtures<<"{\"name\":\""<<names[quarter]<<"_cameraZ"<<sign<<"_pose"<<pose<<"\",\"yaw\":"<<yaw<<",\"pitch\":"<<pitch<<",\"valid\":"<<(valid?"true":"false")<<"}";
            }
            // A face off center, looking directly toward the lens, remains a
            // frontal pose instead of inheriting the camera's optical-axis offset.
            StareCameraVector offCenter={.2,-.15,(double)sign};double yaw,pitch;
            check(StareMobileFaceAngles(offCenter,offCenter,right,up,&yaw,&pitch),"off-center face basis");near(yaw,0,"off-center camera eye contact yaw");near(pitch,0,"off-center camera eye contact pitch");
        }
        StareCameraAffine rotations[]={{1,0,0,1,0,0},{0,1,-1,0,1,0},{-1,0,0,-1,1,1},{0,-1,1,0,0,1}};
        bool portrait=quarter%2==1;
        StareCameraAffine crop=portrait?StareCameraAffine{1,0,0,4.0/3,0,-1.0/6}:StareCameraAffine{4.0/3,0,0,1,-1.0/6,0};
        auto noMirror=StareCameraThen(rotations[quarter],crop);
        for(bool mirrored:{false,true}) {
            auto display=mirrored?StareCameraThen(noMirror,{-1,0,0,1,1,0}):noMirror;
            StareCameraAffine result;check(StareMobileImageTransform(display,640,480,160,&result),"display affine is usable");
            for(Point rawTop:{Point{.5,.5},Point{.3,.3},Point{.7,.3},Point{.3,.7},Point{.7,.7}}) {
                auto expected=rotate(rawTop,quarter);
                if(portrait)expected.y=(expected.y-.5)*4/3+.5;else expected.x=(expected.x-.5)*4/3+.5;
                auto actual=apply({rawTop.x*640,(1-rawTop.y)*480},result);
                near(actual.x,expected.x*160,"upright unmirrored image x for every orientation");
                near(actual.y,(1-expected.y)*160,"top/bottom conversion for every orientation");
            }
        }
    }
    double yaw,pitch;
    check(!StareMobileFaceAngles({0,0,0},{0,0,1},{1,0,0},{0,1,0},&yaw,&pitch),"zero normal cannot become valid face");
    check(!StareMobileFaceAngles({0,0,1},{0,0,0},{1,0,0},{0,1,0},&yaw,&pitch),"zero camera distance cannot become valid face");
    check(!StareMobileFaceAngles({0,0,1},{0,0,1},{0,0,1},{0,1,0},&yaw,&pitch),"degenerate camera axes rejected");
    check(!StareMobileFaceFacing(NAN,0)&&!StareMobileFaceFacing(0,INFINITY),"nonfinite face angles rejected");
    StareCameraAffine output;
    check(!StareMobileImageTransform({0,0,0,0,0,0},640,480,160,&output),"singular image transform rejected");
    check(!StareMobileImageTransform({1,0,0,1,0,0},0,480,160,&output),"empty image rejected");
    check(!StareMobileImageTransform({1,0,0,1,NAN,0},640,480,160,&output),"nonfinite image transform rejected");
    check(StareMobileFrameCanCommit(true,true,4,4,100,100.2),"fresh encoded frame may commit");
    check(StareMobileFrameCanCommit(true,true,4,4,100,100.6),"native frame freshness budget inclusive");
    check(!StareMobileFrameCanCommit(true,true,4,4,100,100.61),"encoder stall cannot refresh an old frame");
    check(!StareMobileFrameCanCommit(true,true,4,6,100,100.2),"A to B to A rotation invalidates an in-flight frame");
    check(!StareMobileFrameCanCommit(false,true,4,4,100,100.2),"camera stop rejects an in-flight frame");
    check(!StareMobileFrameCanCommit(true,false,4,4,100,100.2),"replaced session rejects old frame");
    check(!StareMobileFrameCanCommit(true,true,4,4,100,99),"future capture time cannot become fresh frame");
    check(!StareMobileFrameCanCommit(true,true,4,4,NAN,100),"nonfinite capture time rejected");
    near(StareMobileCaptureObservedAt(100,10,10),100,"fresh callback has no queued capture lag");
    near(StareMobileCaptureObservedAt(100,10,10.4),99.6,"real capture lag is carried into sample and JPEG age");
    check(!StareMobileFrameCanCommit(true,true,1,1,StareMobileCaptureObservedAt(100,10,11),100),"old queued capture cannot become fresh on delivery");
    StareMobileEyeRing ring;StareMobileEyeSample drained[StareMobileEyeRing::Capacity];long long lost;
    for(int i=0;i<4;i++)ring.push({i+1,i/60.0,i/60.0,true,0,i==1||i==2?1.f:0.f,i==1||i==2?1.f:0.f,0,0});
    int drainedCount=ring.drain(drained,&lost);
    check(drainedCount==4&&lost==0,"sample batch keeps every capture even if latest eyes are open");
    check(drained[1].left==1&&drained[2].left==1&&drained[3].left==0,"intermediate closed samples survive a late poll");
    for(int i=0;i<StareMobileEyeRing::Capacity+7;i++)ring.push({i+1,i/60.0,i/60.0,true,0,0,0,0,0});
    drainedCount=ring.drain(drained,&lost);
    check(drainedCount==StareMobileEyeRing::Capacity&&lost==7&&ring.dropped==7,"bounded sample ring explicitly reports overwritten history");
    check(drained[0].sequence==8&&drained[drainedCount-1].sequence==StareMobileEyeRing::Capacity+7,"overflow retains ordered newest capture window");
    check(ring.drain(drained,&lost)==0&&lost==0&&ring.dropped==7,"drain clears only pending overflow and retains diagnostics");
    for(int captureHz:{30,60})for(int renderHz:{5,10,15}) {
        ring.reset();double nextDrain=0;int delivered=0;
        for(int frame=0;frame<captureHz*3;frame++) {
            double time=frame/(double)captureHz;ring.push({frame+1,time,time,true,0,0,0,0,0});
            if(time+.000001>=nextDrain) {
                nextDrain+=1.0/renderHz;delivered+=ring.drain(drained,&lost);check(lost==0,"5/10/15FPS drains never overflow native30/60Hz eye history");
            }
        }
        delivered+=ring.drain(drained,&lost);check(delivered==captureHz*3&&lost==0,"slow render polls consume every captured sample exactly once");
    }
    fixtures<<"]}";
    if(argc>1) { std::ofstream file(argv[1]);file<<fixtures.str();if(!file)throw std::runtime_error("Cannot write native pose fixtures"); }
    std::cout<<checks<<" native camera pose/orientation/mirroring checks passed; 32 native pose fixtures generated"<<std::endl;
}
