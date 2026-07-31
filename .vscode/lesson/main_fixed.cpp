#include <easyx.h>
#include <conio.h>
#include <ctime>
#include <cmath>
#include <cstdlib>
#include <cstdio>
#include <algorithm>
#include <gdiplus.h>
#pragma comment(lib, "gdiplus.lib")

const double PI = 3.14159265358979323846;
inline int t2p(double tiles) { return (int)(tiles * 440.0 / 78.125); }

void makeMaskAndColor(const IMAGE* src, IMAGE* mask, IMAGE* color) {
    int w = src->getwidth(), h = src->getheight();
    *mask = IMAGE(w, h); *color = IMAGE(w, h);
    DWORD* pSrc = GetImageBuffer((IMAGE*)src);
    DWORD* pMask = GetImageBuffer(mask);
    DWORD* pColor = GetImageBuffer(color);
    for (int i = 0; i < w * h; i++) {
        DWORD px = pSrc[i];
        BYTE r = (px >> 16) & 0xFF, g = (px >> 8) & 0xFF, b = px & 0xFF;
        if ((r < 20 && g < 20 && b < 20) || (r > 235 && g > 235 && b > 235)) {
            pMask[i] = 0x00FFFFFF; pColor[i] = 0x00000000;
        } else { pMask[i] = 0x00000000; pColor[i] = px; }
    }
}
void putimageT(int x, int y, const IMAGE* src) {
    IMAGE mask, color; makeMaskAndColor(src, &mask, &color);
    putimage(x, y, &mask, SRCAND); putimage(x, y, &color, SRCPAINT);
}
void putimageT(int x, int y, int dstW, int dstH, const IMAGE* src) {
    IMAGE mask, color; makeMaskAndColor(src, &mask, &color);
    putimage(x, y, dstW, dstH, &mask, 0, 0, SRCAND);
    putimage(x, y, dstW, dstH, &color, 0, 0, SRCPAINT);
}
void putimageT_alpha(int x, int y, const IMAGE* src, double alpha) {
    if (alpha >= 1.0) { putimageT(x, y, src); return; }
    if (alpha <= 0.0) return;
    int w = src->getwidth(), h = src->getheight();
    IMAGE dst(w, h); getimage(&dst, x, y, w, h);
    DWORD* pSrc = GetImageBuffer((IMAGE*)src), *pDst = GetImageBuffer(&dst);
    IMAGE out(w, h); DWORD* pOut = GetImageBuffer(&out);
    for (int i = 0; i < w * h; i++) {
        DWORD sp = pSrc[i];
        if ((sp & 0x00FFFFFF) == 0) { pOut[i] = pDst[i]; }
        else {
            BYTE sr = (sp >> 16) & 0xFF, sg = (sp >> 8) & 0xFF, sb = sp & 0xFF;
            BYTE dr = (pDst[i] >> 16) & 0xFF, dg = (pDst[i] >> 8) & 0xFF, db = pDst[i] & 0xFF;
            pOut[i] = RGB((BYTE)(sr*alpha+dr*(1.0-alpha)),(BYTE)(sg*alpha+dg*(1.0-alpha)),(BYTE)(sb*alpha+db*(1.0-alpha)));
        }
    }
    putimage(x, y, &out);
}
void putimageT_rotated(int cx, int cy, int drawW, int drawH, const IMAGE* src, double angle) {
    int w = src->getwidth(), h = src->getheight();
    DWORD* pSrc = GetImageBuffer((IMAGE*)src);
    Gdiplus::Bitmap bmp(w, h, PixelFormat32bppARGB);
    Gdiplus::BitmapData bmpData; Gdiplus::Rect rect(0, 0, w, h);
    bmp.LockBits(&rect, Gdiplus::ImageLockModeWrite, PixelFormat32bppARGB, &bmpData);
    BYTE* pDst = (BYTE*)bmpData.Scan0;
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
        DWORD px = pSrc[y * w + x]; int idx = y * bmpData.Stride + x * 4;
        BYTE r = (px >> 16) & 0xFF, g = (px >> 8) & 0xFF, b = px & 0xFF;
        if ((r < 20 && g < 20 && b < 20) || (r > 235 && g > 235 && b > 235)) { pDst[idx+3] = 0; }
        else { pDst[idx+0]=b; pDst[idx+1]=g; pDst[idx+2]=r; pDst[idx+3]=255; }
    }
    bmp.UnlockBits(&bmpData);
    HDC screenDC = GetImageHDC(NULL);
    Gdiplus::Graphics graphics(screenDC);
    graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBilinear);
    graphics.TranslateTransform((float)cx, (float)cy);
    graphics.RotateTransform((float)(angle * 180.0 / PI));
    graphics.ScaleTransform((float)drawW / w, (float)drawH / h);
    graphics.DrawImage(&bmp, -w / 2, -h / 2, w, h);
}

enum BossState { NORMAL, DASH_WINDUP, DASH, DASH_PAUSE, SKILL2_PREP, SKILL2_BURST, SKILL3, SKILL4 };
enum ProjType { PROJ_SULFUR=0,PROJ_FISSION=1,PROJ_ABYSS=2,PROJ_WRAITH=3,PROJ_BHELL=4,PROJ_FIST1=5,PROJ_FIST2=6,PROJ_BLADE1=7,PROJ_BLADE2=8,PROJ_SKULL=9,PROJ_FIRE=10 };
struct Projectile { bool active; ProjType type; double x,y,vx,vy,timer,accel,baseY; };
bool rectCollide(double x1,double y1,double w1,double h1,double x2,double y2,double w2,double h2) {
    return (x1-w1/2)<(x2+w2/2)&&(x1+w1/2)>(x2-w2/2)&&(y1-h1/2)<(y2+h2/2)&&(y1+h1/2)>(y2-h2/2);
}

int main() { {
    const int WIN_W=1500, WIN_H=1000;
    initgraph(WIN_W, WIN_H);
    Gdiplus::GdiplusStartupInput gpsi; ULONG_PTR gdpToken;
    Gdiplus::GdiplusStartup(&gdpToken, &gpsi, NULL);

    const int CX=WIN_W/2, CY=WIN_H/2;
    const double FRAME_DT=1.0/60.0; const int FRAME_MS=1000/60;
    clock_t lastClock=clock();

    bool practiceMode=false;
    const double PL_HP_NORMAL=500.0, PL_HP_PRACTICE=99999.0;
    double playerHP=PL_HP_NORMAL, playerMaxHP=PL_HP_NORMAL;

    const int ARENA_HALF=440;
    const int ARENA_L=CX-ARENA_HALF, ARENA_T=CY-ARENA_HALF, ARENA_R=CX+ARENA_HALF, ARENA_B=CY+ARENA_HALF;

    IMAGE imgPL,imgPR,imgBL,imgBR,imgBWL,imgBWR,imgPA,imgPB,imgPC,imgPD;
    IMAGE imgBrAL,imgBrAR,imgBrBL,imgBrBR,imgF1,imgF2,imgBd1,imgBd2,imgSk,imgEye,imgFire,imgMoon;
    IMAGE imgWH,imgWBA,imgWBB,imgWT,imgHeart;
    loadimage(&imgPL,_T("assets\\playerleft.png")); loadimage(&imgPR,_T("assets\\playerright.png"));
    loadimage(&imgBL,_T("assets\\zaieleft.png")); loadimage(&imgBR,_T("assets\\zaieright.png"));
    loadimage(&imgBWL,_T("assets\\wudileft.png")); loadimage(&imgBWR,_T("assets\\wudiright.png"));
    loadimage(&imgPA,_T("assets\\a.png")); loadimage(&imgPB,_T("assets\\b.png"));
    loadimage(&imgPC,_T("assets\\c.png")); loadimage(&imgPD,_T("assets\\d.png"));
    loadimage(&imgBrAL,_T("assets\\aleft.png")); loadimage(&imgBrAR,_T("assets\\aright.png"));
    loadimage(&imgBrBL,_T("assets\\bleft.png")); loadimage(&imgBrBR,_T("assets\\bright.png"));
    loadimage(&imgF1,_T("assets\\a1.png")); loadimage(&imgF2,_T("assets\\a2.png"));
    loadimage(&imgBd1,_T("assets\\b1.png")); loadimage(&imgBd2,_T("assets\\b2.png"));
    loadimage(&imgSk,_T("assets\\e.png")); loadimage(&imgEye,_T("assets\\eye.png"));
    loadimage(&imgFire,_T("assets\\fire.png")); loadimage(&imgMoon,_T("assets\\moon.png"));
    loadimage(&imgWH,_T("assets\\head.png")); loadimage(&imgWBA,_T("assets\\bodya.png"));
    loadimage(&imgWBB,_T("assets\\bodyb.png")); loadimage(&imgWT,_T("assets\\tail.png"));
    loadimage(&imgHeart,_T("assets\\heart.png"));

    const int PLAYER_W=__max(__max(imgPL.getwidth(),imgPR.getwidth()),1);
    const int PLAYER_H=__max(__max(imgPL.getheight(),imgPR.getheight()),1);
    const int BOSS_W=__max(__max(__max(imgBL.getwidth(),imgBR.getwidth()),imgBWL.getwidth()),imgBWR.getwidth());
    const int BOSS_H=__max(__max(__max(imgBL.getheight(),imgBR.getheight()),imgBWL.getheight()),imgBWR.getheight());
    const int PROJ_A_W=imgPA.getwidth(),PROJ_A_H=imgPA.getheight();
    const int PROJ_B_W=imgPB.getwidth(),PROJ_B_H=imgPB.getheight();
    const int PROJ_C_W=imgPC.getwidth(),PROJ_C_H=imgPC.getheight();

    double playerX=CX, playerY=ARENA_B-80; bool facingRight=true;
    const double PLAYER_SPEED=61.0*440.0/78.125;
    const double PL_HB_W=t2p(2), PL_HB_H=t2p(3);
    const double INVINCIBLE_TIME=0.8; double invincibleTimer=0.0; bool playerVisible=true;
    const double FIRE_SPEED=300.0*440.0/78.125; double fireCooldown=0.0;
    const double DMG_SULFUR=60,DMG_FISSION=110,DMG_DASH=130,DMG_WRAITH=90,DMG_SKULL=90,DMG_FIST=75,DMG_MOON=50;
    auto dmgPlayer=[&](double dmg){if(playerHP>0){playerHP-=dmg;if(playerHP<0)playerHP=0;}};
    bool playerDead=false;
    const char* deathMsgs[]={"Supreme Witch, Calamitas has slain all players","You were consumed by hellfire","You died from fourth-degree burns"};
    int deathMsgIdx=0;
    const double PL_HP_REGEN=5.0, POTION_HEAL=300.0, POTION_CD=60.0; double potionCD=0.0;

    const double PROJ_A_HB_W=PROJ_A_W*0.35, PROJ_A_HB_H=PROJ_A_H*0.35;
    const double PROJ_B_HB_W=PROJ_B_W*0.35, PROJ_B_HB_H=PROJ_B_H*0.35;
    const double PROJ_C_HB_W=PROJ_B_HB_W*1.5, PROJ_C_HB_H=PROJ_B_HB_H*1.5;
    const double PROJ_D_HB_W=PROJ_C_HB_W, PROJ_D_HB_H=PROJ_C_HB_H;

    double bossX=CX, bossY=CY, bossTargetX=CX, bossTargetY=CY; bool bossFacingRight=true;
    int bossPhase=0; double bossPhaseTimer=0.0;
    const double BOSS_PHASE_DURATION=2.0; const int BOSS_ABOVE_Y=t2p(44), BOSS_SIDE_X=t2p(39);
    const double BOSS_RESPONSIVENESS=5.0;

    BossState bossState=NORMAL; int dashesTotal=0,dashesRemaining=0;
    double dashTimer=0,dashAngle=0,dashDist=0,dashTraveled=0,dashTargetX=0,dashTargetY=0;
    bool bossTransparent=false,dashHalf=false; double bossAlpha=1.0;
    const double WINDUP_TIME=0.5,PAUSE_TIME=0.2,DASH_SPEED=200.0*440.0/78.125,TRANSPARENT_ALPHA=0.35;

    int skill2BurstsTotal=0,skill2BurstsRemaining=0; double skill2Timer=0.0;
    const double SKILL2_PREP_TIME=0.4, SKILL2_BURST_INTERVAL=2.0; double skill2CurInterval=2.0;
    const double PROJ_SPEED=37.5*440.0/78.125, FISSION_SPEED=30.0*440.0/78.125;
    const double FISSION_LIFETIME=2.0, FISSION_TURN=3.0; const int FISSION_SPLIT=16;
    const int PROJ_PER_BURST=8; const double FAN_SPREAD=PI*80.0/180.0;

    int skill3BurstsTotal=0,skill3BurstsRemaining=0; double skill3BurstTimer=0.0;
    const double ABYSS_SPEED=20.0*440.0/78.125, ABYSS_LIFETIME=2.5, ABYSS_TURN=2.5;
    const int ABYSS_SPLIT=36; const double ABYSS_BURST_INTERVAL=2.0;

    int skill4BurstsTotal=0,skill4BurstsRemaining=0; double skill4BurstTimer=0.0;
    const double WRAITH_ACCEL=140.0*440.0/78.125, WRAITH_INIT_SPD=40.0*440.0/78.125;
    const double WRAITH_BURST_INTERVAL=0.35; const int WRAITH_SIDE_X=t2p(55);

    enum PatternSkill{PSK_SULFUR,PSK_WRAITH,PSK_ABYSS,PSK_DASH};
    struct PtnStep{PatternSkill skill;int count;};
    const PtnStep PATTERN[]={
        {PSK_SULFUR,-1},{PSK_WRAITH,-1},{PSK_ABYSS,2},{PSK_DASH,4},{PSK_ABYSS,2},{PSK_WRAITH,-1},{PSK_SULFUR,-1},
        {PSK_DASH,2},{PSK_SULFUR,-1},{PSK_WRAITH,-1},{PSK_ABYSS,4},{PSK_WRAITH,-1},{PSK_DASH,2},{PSK_SULFUR,-1},
        {PSK_ABYSS,4},{PSK_DASH,4},{PSK_SULFUR,-1},{PSK_DASH,2},{PSK_SULFUR,-1},{PSK_DASH,2},{PSK_SULFUR,-1}
    };
    const int PATTERN_LEN=sizeof(PATTERN)/sizeof(PATTERN[0]);
    int patternIndex=0; double patternCooldown=1.5;

    bool phase2=false,phase2Opening=false;

    bool bulletHellActive=false; double bulletHellSpawnTimer=0.0,bhTimer=-1.0;
    const double BHELL_SPEED=20.0*440.0/78.125;
    const double BHELL_SPAWN_MIN=0.09375,BHELL_SPAWN_MAX=0.16875,BHELL_SPREAD=t2p(18);
    int bhOccurrence=0; double bhSpecTimer=0.0,bhSpecInterval=3.5;
    bool bhSpecToggle=false,bhSpecAlt=false,bhSkulls=false;

    bool moonActive=false; const double MOON_SCALE=t2p(60)/824.0;
    const int MOON_SIZE_W=(int)(imgMoon.getwidth()*MOON_SCALE), MOON_SIZE_H=(int)(imgMoon.getheight()*MOON_SCALE);
    const double MOON_COLLIDE_R=t2p(30), MOON_SPEED=12.0*440.0/78.125;
    double moonX=CX,moonY=CY,moonDmgDelay=0.0;

    bool brothersActive=false;
    struct Brother{double x,y;bool alive,onLeft;double hp;IMAGE*imgLeft,*imgRight;};
    Brother broA={0,0,true,false,100.0,&imgBrAL,&imgBrAR}, broB={0,0,true,true,100.0,&imgBrBL,&imgBrBR};
    double broAttackTimer=2.0; int broFistToggle=0,broFistCount=0; bool broAttacking=false;
    const double BRO_DISTANCE=t2p(70),BRO_SPAWN_DIST=t2p(50);
    const int BRO_FIST_TOTAL=18; const double BRO_FIST_INTV_START=0.7,BRO_FIST_INTV_END=0.3;
    const double BLADE_FAST_SPEED=100.0*440.0/78.125; int broBladeToggle=0;

    bool eyesActive=false; const int EYE_COUNT=10;
    const int EYE_SIZE_W=(int)(PROJ_C_HB_W),EYE_SIZE_H=(int)(PROJ_C_HB_H);
    const double EYE_DIST=t2p(10),EYE_FIRE_INTERVAL=4.0; double eyeFireTimer=0.0;
    struct Eye{double x,y,angle;int hp;bool alive;};
    Eye eyes[EYE_COUNT];
    for(int i=0;i<EYE_COUNT;i++){eyes[i].angle=i*2.0*PI/EYE_COUNT;eyes[i].hp=100;eyes[i].alive=false;}

    bool skullActive=false; double skullSpawnTimer=0.0;
    const double SKULL_SPEED=BHELL_SPEED,SKULL_AMP=t2p(2),SKULL_PERIOD=1.0;
    const double SKULL_SPAWN_MIN=BHELL_SPAWN_MIN*4.0,SKULL_SPAWN_MAX=BHELL_SPAWN_MAX*4.0;

    const int HEART_COUNT=10;
    struct Heart{double x,y;bool active;double hp;};
    Heart hearts[HEART_COUNT];
    const int HEART_SIZE_W=(int)(PROJ_A_HB_W*3.0),HEART_SIZE_H=(int)(PROJ_A_HB_H*2.5);
    const int HEART_DX=t2p(15),HEART_DY=t2p(3);
    const double HEART_APEX_Y=(double)(ARENA_T+80);
    for(int i=0;i<HEART_COUNT;i++){
        int side=(i<5)?-1:1, idx=(i<5)?(4-i):(i-5);
        hearts[i].x=(double)CX+side*HEART_DX*(idx+0.5);
        hearts[i].y=HEART_APEX_Y+HEART_DY*(idx+0.5); hearts[i].active=false; hearts[i].hp=100;
    }

    const int WORM_SEG_SIZE=t2p(9),WORM_MAX_SEG=40;
    struct WormSegment{double x,y;IMAGE*img;};
    WormSegment worm[WORM_MAX_SEG]; int wormLen=0;
    enum WormState{WORM_RANDOM,WORM_TRACK}; WormState wormState=WORM_RANDOM;
    double wormStateTimer=4.0,wormHeadVx=0,wormHeadVy=0,wormTurnTimer=0.0;
    const double WORM_SPEED=45.0*440.0/78.125,WORM_RANDOM_TURN=0.6,WORM_RING_ACCEL=10.0*440.0/78.125;
    auto initWorm=[&](){if(wormLen==0)return;
        for(int i=0;i<wormLen;i++){
            if(i==wormLen-1)worm[i].img=&imgWH;else if(i==0)worm[i].img=&imgWT;
            else if(i%2==0)worm[i].img=&imgWBA;else worm[i].img=&imgWBB;
            worm[i].x=(double)(ARENA_L+100+i*WORM_SEG_SIZE);worm[i].y=(double)(ARENA_T+80);
        }
    };

    const double BOSS_MAX_HP=100.0; double bossHP=BOSS_MAX_HP;
    bool bossWeak=false;
    bool f_bh1=false,f_w1=false,f_w1d=false,f_bh2=false,f_bh3=false,f_bro=false,f_broD=false,f_p2=false;
    bool f_bh4=false,f_eye=false,f_eyeD=false,f_bh5=false,f_w2=false,f_w2d=false,f_end=false;

    const int MAX_PROJ=256; Projectile proj[MAX_PROJ];
    for(int i=0;i<MAX_PROJ;i++){proj[i].active=false;proj[i].type=PROJ_SULFUR;proj[i].accel=0.0;proj[i].baseY=0.0;}

    srand((unsigned)time(NULL));

    const double FIRE_DMG_BOSS=0.1,FIRE_DMG_BRO=0.5,FIRE_DMG_SMALL=10.0;
    int dashSeq=0;
    auto resetBossFight=[&](){
        playerHP=playerMaxHP;playerDead=false;invincibleTimer=0.0;playerVisible=true;
        bossHP=BOSS_MAX_HP;potionCD=0.0;
        f_bh1=f_w1=f_w1d=f_bh2=f_bh3=f_bro=f_broD=f_p2=f_bh4=f_eye=f_eyeD=f_bh5=f_w2=f_w2d=f_end=false;
        phase2=false;phase2Opening=false;bulletHellActive=false;bhTimer=-1.0;
        brothersActive=false;eyesActive=false;moonActive=false;bossWeak=false;bhSkulls=false;skullActive=false;
        wormLen=0;for(int i=0;i<HEART_COUNT;i++)hearts[i].active=false;
        for(int i=0;i<EYE_COUNT;i++)eyes[i].alive=false;
        for(int i=0;i<MAX_PROJ;i++)proj[i].active=false;
        bossX=CX;bossY=CY;patternIndex=0;patternCooldown=1.5;dashesRemaining=0;
    };

    BeginBatchDraw();
    bool lWasDown=false;
    while(!(GetAsyncKeyState(VK_ESCAPE)&0x8000)){
        clock_t frameStart=clock();
        double dt=(double)(frameStart-lastClock)/CLOCKS_PER_SEC; lastClock=frameStart;
        if(dt>0.1)dt=FRAME_DT;

        bool left=(GetAsyncKeyState('A')&0x8000)!=0, right=(GetAsyncKeyState('D')&0x8000)!=0;
        bool up=(GetAsyncKeyState('W')&0x8000)!=0||(GetAsyncKeyState(VK_SPACE)&0x8000)!=0;
        bool down=(GetAsyncKeyState('S')&0x8000)!=0;
        int step=(int)(PLAYER_SPEED*dt+0.5);
        if(left)playerX-=step; if(right)playerX+=step; if(up)playerY-=step; if(down)playerY+=step;
        if(left&&!right)facingRight=false; else if(right&&!left)facingRight=true;
        int halfW=PLAYER_W/2,halfH=PLAYER_H/2;
        if(playerX-halfW<ARENA_L)playerX=(double)(ARENA_L+halfW);
        if(playerX+halfW>ARENA_R)playerX=(double)(ARENA_R-halfW);
        if(playerY-halfH<ARENA_T)playerY=(double)(ARENA_T+halfH);
        if(playerY+halfH>ARENA_B)playerY=(double)(ARENA_B-halfH);

        fireCooldown-=dt;
        if((GetAsyncKeyState(VK_LBUTTON)&0x8000)&&fireCooldown<=0.0){
            fireCooldown=2.0/45.0; POINT mp; GetCursorPos(&mp); ScreenToClient(GetHWnd(),&mp);
            double a=std::atan2(mp.y-playerY,mp.x-playerX);
            for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){
                proj[j].type=PROJ_FIRE; proj[j].x=playerX; proj[j].y=playerY;
                proj[j].vx=std::cos(a)*FIRE_SPEED; proj[j].vy=std::sin(a)*FIRE_SPEED;
                proj[j].timer=0;proj[j].accel=0;proj[j].active=true;break;
            }
        }

        if(playerHP<playerMaxHP){playerHP+=PL_HP_REGEN*dt;if(playerHP>playerMaxHP)playerHP=playerMaxHP;}
        if(potionCD>0.0)potionCD-=dt;
        static bool hPotDown=false;
        {bool hD=(GetAsyncKeyState('H')&0x8000)!=0;
        if(hD&&!hPotDown&&potionCD<=0.0){playerHP+=POTION_HEAL;if(playerHP>playerMaxHP)playerHP=playerMaxHP;potionCD=POTION_CD;}
        hPotDown=hD;}

        if(playerHP<=0.0&&!playerDead){playerDead=true;deathMsgIdx=rand()%3;}
        if(playerDead){
            cleardevice(); setfillcolor(BLACK); solidrectangle(0,0,WIN_W,WIN_H);
            settextcolor(RED); settextstyle(32,0,_T("Consolas"));
            const char* msg=deathMsgs[deathMsgIdx]; outtextxy(CX-textwidth(msg)/2,CY-60,msg);
            settextcolor(WHITE); settextstyle(24,0,_T("Consolas"));
            const char* rst="Press R to restart"; outtextxy(CX-textwidth(rst)/2,CY+10,rst);
            FlushBatchDraw();
            if(GetAsyncKeyState('R')&0x8000)resetBossFight();
            int el=(int)((clock()-frameStart)*1000/CLOCKS_PER_SEC);
            int rm=FRAME_MS-el; if(rm>0)Sleep(rm); continue;
        }

        {bool lD=(GetAsyncKeyState('L')&0x8000)!=0;
        if(lD&&!lWasDown){
            if(wormLen>0||brothersActive||eyesActive){
                if(wormLen>0){wormLen=0;for(int i=0;i<HEART_COUNT;i++)hearts[i].active=false;}
                if(brothersActive)brothersActive=false;
                if(eyesActive){eyesActive=false;for(int i=0;i<EYE_COUNT;i++)eyes[i].alive=false;}
            }else{bossHP-=5.0;if(bossHP<0)bossHP=0;}
        }lWasDown=lD;}

        if(bulletHellActive){bhTimer-=dt;
            if(bhTimer<=0.0){bulletHellActive=false;bhTimer=-1.0;
                if(bhSkulls){bhSkulls=false;skullActive=false;}
                for(int i=0;i<MAX_PROJ;i++)if(proj[i].active&&(proj[i].type==PROJ_BHELL||proj[i].type==PROJ_SKULL))proj[i].active=false;
            }
        }
        if(moonActive&&moonDmgDelay>0.0)moonDmgDelay-=dt;
        if(bulletHellActive&&bhOccurrence>=2){bhSpecTimer-=dt;
            if(bhSpecTimer<=0.0){bhSpecTimer=bhSpecInterval;
                ProjType sp=bhSpecAlt?(bhSpecToggle?PROJ_ABYSS:PROJ_FISSION):PROJ_FISSION;
                if(bhSpecAlt)bhSpecToggle=!bhSpecToggle;
                double a=std::atan2(playerY-bossY,playerX-bossX);
                double spd=(sp==PROJ_ABYSS)?ABYSS_SPEED:FISSION_SPEED;
                double lf=(sp==PROJ_ABYSS)?ABYSS_LIFETIME:FISSION_LIFETIME;
                for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){
                    proj[j].type=sp;proj[j].x=bossX;proj[j].y=bossY;
                    proj[j].vx=std::cos(a)*spd;proj[j].vy=std::sin(a)*spd;
                    proj[j].timer=lf;proj[j].accel=0.0;proj[j].active=true;break;
                }
            }
        }

        if(!f_bh1){f_bh1=true;bhOccurrence=1;bulletHellActive=true;bhTimer=15.0;bhSkulls=false;}
        if(f_bh1&&!bulletHellActive&&!f_w1){f_w1=true;bossWeak=true;wormLen=16;initWorm();for(int i=0;i<HEART_COUNT;i++)hearts[i].active=true;hearts[i].hp=100;}
        if(f_w1&&!f_w1d){bool ad=true;for(int i=0;i<HEART_COUNT;i++)if(hearts[i].active)ad=false;if(ad){f_w1d=true;wormLen=0;bossWeak=false;}}
        if(!f_bh2&&f_w1d&&bossHP<75.0){f_bh2=true;bhOccurrence=2;bulletHellActive=true;bhTimer=15.0;bhSpecInterval=3.5;bhSpecAlt=false;bhSpecTimer=0.0;bhSkulls=false;}
        if(!f_bh3&&f_bh2&&!bulletHellActive&&bossHP<50.0){f_bh3=true;bhOccurrence=3;bulletHellActive=true;bhTimer=15.0;bhSpecInterval=2.5;bhSpecAlt=true;bhSpecToggle=false;bhSpecTimer=0.0;bhSkulls=false;}
        if(!f_bro&&f_bh3&&!bulletHellActive&&bossHP<45.0){f_bro=true;brothersActive=true;}
        if(f_bro&&!f_broD&&!brothersActive){f_broD=true;f_p2=true;phase2=true;phase2Opening=true;}
        if(!f_bh4&&f_p2&&bossHP<30.0){f_bh4=true;bhOccurrence=4;bulletHellActive=true;bhTimer=15.0;bhSpecInterval=2.5;bhSpecAlt=true;bhSpecToggle=false;bhSpecTimer=0.0;bhSkulls=false;moonActive=true;moonDmgDelay=2.5;moonX=CX;moonY=CY;}
        if(!f_eye&&f_bh4&&!bulletHellActive&&bossHP<20.0){f_eye=true;eyesActive=true;}
        if(f_eye&&!f_eyeD&&!eyesActive){f_eyeD=true;}
        if(!f_bh5&&f_eyeD&&bossHP<10.0){f_bh5=true;bhOccurrence=5;bulletHellActive=true;bhTimer=15.0;bhSpecInterval=3.5;bhSpecAlt=true;bhSpecToggle=false;bhSpecTimer=0.0;bhSkulls=true;skullActive=true;}
        if(!f_w2&&f_bh5&&!bulletHellActive&&bossHP<8.0){f_w2=true;bossWeak=true;wormLen=16;initWorm();for(int i=0;i<HEART_COUNT;i++){hearts[i].active=true;hearts[i].hp=100;}}
        if(f_w2&&!f_w2d){bool ad=true;for(int i=0;i<HEART_COUNT;i++)if(hearts[i].active)ad=false;if(ad){f_w2d=true;wormLen=0;bossWeak=false;}}
        if(!f_end&&bossHP<1.0){f_end=true;bulletHellActive=false;moonActive=false;brothersActive=false;eyesActive=false;bhSkulls=false;for(int i=0;i<MAX_PROJ;i++)proj[i].active=false;bossX=CX;bossY=CY;}

        bossTransparent=false;bossAlpha=1.0;
        if(!bulletHellActive&&!brothersActive&&!f_end){
        switch(bossState){
        case NORMAL:{
            bossPhaseTimer-=dt;if(bossPhaseTimer<=0.0){bossPhase=(bossPhase+1)%4;bossPhaseTimer=BOSS_PHASE_DURATION;}
            switch(bossPhase){case 0:case 2:bossTargetX=playerX;bossTargetY=playerY-BOSS_ABOVE_Y;break;
                case 1:bossTargetX=playerX-BOSS_SIDE_X;bossTargetY=playerY;break;
                case 3:bossTargetX=playerX+BOSS_SIDE_X;bossTargetY=playerY;break;}
            {double dx=bossTargetX-bossX,dy=bossTargetY-bossY,dist=std::sqrt(dx*dx+dy*dy);
                double t=1.0-std::exp(-BOSS_RESPONSIVENESS*dt);
                if(dist<1.0){bossX=bossTargetX;bossY=bossTargetY;}else{bossX+=dx*t;bossY+=dy*t;}}
            if(phase2Opening){phase2Opening=false;patternIndex=PATTERN_LEN-1;bossState=SKILL2_PREP;skill2Timer=SKILL2_PREP_TIME;skill2BurstsTotal=4;skill2BurstsRemaining=4;skill2CurInterval=SKILL2_BURST_INTERVAL-0.5;}
            else{patternCooldown-=dt;
                if(patternCooldown<=0.0){const PtnStep& step=PATTERN[patternIndex];int cnt=step.count;
                    switch(step.skill){
                    case PSK_SULFUR:bossState=SKILL2_PREP;skill2Timer=SKILL2_PREP_TIME;skill2BurstsTotal=(cnt>0)?cnt:((rand()%2)+3);skill2BurstsRemaining=skill2BurstsTotal;skill2CurInterval=SKILL2_BURST_INTERVAL;break;
                    case PSK_WRAITH:bossState=SKILL4;skill4BurstsTotal=(cnt>0)?cnt:20;if(bossWeak)skill4BurstsTotal=(skill4BurstsTotal+1)/2;skill4BurstsRemaining=skill4BurstsTotal;skill4BurstTimer=0.0;break;
                    case PSK_ABYSS:bossState=SKILL3;skill3BurstsTotal=(cnt>0)?cnt:((rand()%2)+3);skill3BurstsRemaining=skill3BurstsTotal;skill3BurstTimer=0.0;break;
                    case PSK_DASH:bossState=DASH_WINDUP;dashTimer=WINDUP_TIME;dashesTotal=(cnt>0)?cnt:((rand()%2)+2);if(phase2)dashesTotal=(dashesTotal+1)/2;dashesRemaining=dashesTotal;dashHalf=(patternIndex==7||patternIndex==12||patternIndex==17||patternIndex==19);bossTransparent=true;bossAlpha=TRANSPARENT_ALPHA;break;
                    }
                }
            }
            break;}
        case SKILL2_PREP:{bossTargetX=playerX;bossTargetY=playerY-BOSS_ABOVE_Y;
            {double dx=bossTargetX-bossX,dy=bossTargetY-bossY,dist=std::sqrt(dx*dx+dy*dy);double t=1.0-std::exp(-BOSS_RESPONSIVENESS*dt);if(dist<1.0){bossX=bossTargetX;bossY=bossTargetY;}else{bossX+=dx*t;bossY+=dy*t;}}
            skill2Timer-=dt;if(skill2Timer<=0.0){bossState=SKILL2_BURST;skill2Timer=0.0;}break;}
        case SKILL2_BURST:{bossTargetX=playerX;bossTargetY=playerY-BOSS_ABOVE_Y;
            {double dx=bossTargetX-bossX,dy=bossTargetY-bossY,dist=std::sqrt(dx*dx+dy*dy);double t=1.0-std::exp(-BOSS_RESPONSIVENESS*dt);if(dist<1.0){bossX=bossTargetX;bossY=bossTargetY;}else{bossX+=dx*t;bossY+=dy*t;}}
            skill2Timer-=dt;if(skill2Timer<=0.0&&skill2BurstsRemaining>0){
                if((rand()%100)<30){for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){double a=std::atan2(playerY-bossY,playerX-bossX);proj[j].type=PROJ_FISSION;proj[j].x=bossX;proj[j].y=bossY;proj[j].vx=std::cos(a)*FISSION_SPEED;proj[j].vy=std::sin(a)*FISSION_SPEED;proj[j].timer=FISSION_LIFETIME;proj[j].accel=0.0;proj[j].active=true;break;}}
                else{double cA=std::atan2(playerY-bossY,playerX-bossX);double hS=FAN_SPREAD/2.0;
                    for(int i=0;i<PROJ_PER_BURST;i++)for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){double a=cA-hS+i*FAN_SPREAD/(PROJ_PER_BURST-1);proj[j].type=PROJ_SULFUR;proj[j].x=bossX;proj[j].y=bossY;proj[j].vx=std::cos(a)*PROJ_SPEED;proj[j].vy=std::sin(a)*PROJ_SPEED;proj[j].timer=0.0;proj[j].accel=0.0;proj[j].active=true;break;}
                }
                skill2BurstsRemaining--;skill2Timer=skill2CurInterval;
            }
            if(skill2BurstsRemaining<=0&&skill2Timer<=0.0){bossState=NORMAL;patternIndex=(patternIndex+1)%PATTERN_LEN;patternCooldown=1.5;}break;}
        case SKILL3:{bossTargetX=playerX;bossTargetY=playerY-BOSS_ABOVE_Y;
            {double dx=bossTargetX-bossX,dy=bossTargetY-bossY,dist=std::sqrt(dx*dx+dy*dy);double t=1.0-std::exp(-BOSS_RESPONSIVENESS*dt);if(dist<1.0){bossX=bossTargetX;bossY=bossTargetY;}else{bossX+=dx*t;bossY+=dy*t;}}
            skill3BurstTimer-=dt;if(skill3BurstTimer<=0.0&&skill3BurstsRemaining>0){double a=std::atan2(playerY-bossY,playerX-bossX);
                for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_ABYSS;proj[j].x=bossX;proj[j].y=bossY;proj[j].vx=std::cos(a)*ABYSS_SPEED;proj[j].vy=std::sin(a)*ABYSS_SPEED;proj[j].timer=ABYSS_LIFETIME;proj[j].accel=0.0;proj[j].active=true;break;}
                skill3BurstsRemaining--;skill3BurstTimer=ABYSS_BURST_INTERVAL;}
            if(skill3BurstsRemaining<=0&&skill3BurstTimer<=0.0){bossState=NORMAL;patternIndex=(patternIndex+1)%PATTERN_LEN;patternCooldown=1.5;}break;}
        case SKILL4:{bool fireRight=(bossX>=playerX);bossTargetX=playerX+(fireRight?WRAITH_SIDE_X:-WRAITH_SIDE_X);bossTargetY=playerY;
            {double dx=bossTargetX-bossX,dy=bossTargetY-bossY,dist=std::sqrt(dx*dx+dy*dy);double t=1.0-std::exp(-BOSS_RESPONSIVENESS*2.0*dt);if(dist<1.0){bossX=bossTargetX;bossY=bossTargetY;}else{bossX+=dx*t;bossY+=dy*t;}}
            skill4BurstTimer-=dt;if(skill4BurstTimer<=0.0&&skill4BurstsRemaining>0){double dir=(playerX>bossX)?1.0:-1.0;
                for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_WRAITH;proj[j].x=bossX;proj[j].y=bossY;proj[j].vx=dir*WRAITH_INIT_SPD;proj[j].vy=0.0;proj[j].timer=0.0;proj[j].accel=0.0;proj[j].active=true;break;}
                skill4BurstsRemaining--;skill4BurstTimer=WRAITH_BURST_INTERVAL;}
            if(skill4BurstsRemaining<=0&&skill4BurstTimer<=0.0){bossState=NORMAL;patternIndex=(patternIndex+1)%PATTERN_LEN;patternCooldown=1.5;}break;}
        case DASH_WINDUP:{bossTransparent=true;bossAlpha=TRANSPARENT_ALPHA;dashTimer-=dt;
            if(dashTimer<=0.0){dashTargetX=playerX;dashTargetY=playerY;double dx=dashTargetX-bossX,dy=dashTargetY-bossY;dashAngle=std::atan2(dy,dx);dashDist=dashHalf?t2p(50):t2p(100);dashTraveled=0.0;bossState=DASH;}break;}
        case DASH:{double move=DASH_SPEED*dt;bossX+=std::cos(dashAngle)*move;bossY+=std::sin(dashAngle)*move;dashTraveled+=move;
            if(!(invincibleTimer>0.0)&&rectCollide(bossX,bossY,(double)BOSS_W/4.0,(double)BOSS_H/4.0,playerX,playerY,(double)PLAYER_W,(double)PLAYER_H)){dmgPlayer(DMG_DASH);invincibleTimer=INVINCIBLE_TIME;}
            if(dashTraveled>=dashDist){dashesRemaining--;if(dashesRemaining>0){bossState=DASH_PAUSE;dashTimer=PAUSE_TIME;}else{bossState=NORMAL;patternIndex=(patternIndex+1)%PATTERN_LEN;patternCooldown=1.5;}}break;}
        case DASH_PAUSE:{dashTimer-=dt;if(dashTimer<=0.0){bossState=DASH_WINDUP;dashTimer=WINDUP_TIME;bossTransparent=true;bossAlpha=TRANSPARENT_ALPHA;}break;} }
        if(bossState!=DASH_WINDUP&&bossState!=DASH)bossFacingRight=(bossX>=playerX);

        if(wormLen>0){wormStateTimer-=dt;
            if(wormState==WORM_RANDOM){wormTurnTimer-=dt;double tA=std::atan2(wormHeadVy,wormHeadVx);
                if(wormTurnTimer<=0.0){wormTurnTimer=WORM_RANDOM_TURN;tA=(rand()%6283)/1000.0;}
                int sz=WORM_SEG_SIZE/2;if(worm[wormLen-1].x-sz<ARENA_L&&wormHeadVx<0)tA=0;
                if(worm[wormLen-1].x+sz>ARENA_R&&wormHeadVx>0)tA=PI;
                if(worm[wormLen-1].y-sz<ARENA_T&&wormHeadVy<0)tA=PI/2.0;
                if(worm[wormLen-1].y+sz>ARENA_B&&wormHeadVy>0)tA=-PI/2.0;
                double cA=std::atan2(wormHeadVy,wormHeadVx),diff=tA-cA;
                while(diff>PI)diff-=2*PI;while(diff<-PI)diff+=2*PI;double mT=3.0*dt;
                if(diff>mT)diff=mT;else if(diff<-mT)diff=-mT;double nA=cA+diff;
                wormHeadVx=std::cos(nA)*WORM_SPEED;wormHeadVy=std::sin(nA)*WORM_SPEED;
                if(wormStateTimer<=0.0)wormState=WORM_TRACK;
            }else{double dx=bossX-worm[wormLen-1].x,dy=bossY-worm[wormLen-1].y,dist=std::sqrt(dx*dx+dy*dy),dA=std::atan2(dy,dx);
                double cA=std::atan2(wormHeadVy,wormHeadVx),diff=dA-cA;
                while(diff>PI)diff-=2*PI;while(diff<-PI)diff+=2*PI;double mT=3.0*dt;
                if(diff>mT)diff=mT;else if(diff<-mT)diff=-mT;double nA=cA+diff;
                wormHeadVx=std::cos(nA)*WORM_SPEED;wormHeadVy=std::sin(nA)*WORM_SPEED;
                if(dist<(BOSS_W+WORM_SEG_SIZE)/2.0){for(int k=0;k<26;k++){double a=k*2.0*PI/26;for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_SULFUR;proj[j].x=bossX;proj[j].y=bossY;proj[j].vx=std::cos(a)*PROJ_SPEED;proj[j].vy=std::sin(a)*PROJ_SPEED;proj[j].timer=0.0;proj[j].accel=WORM_RING_ACCEL;proj[j].active=true;break;}}wormState=WORM_RANDOM;wormStateTimer=4.0;}
            }
            worm[wormLen-1].x+=wormHeadVx*dt;worm[wormLen-1].y+=wormHeadVy*dt;
            for(int i=wormLen-2;i>=0;i--){double dx=worm[i+1].x-worm[i].x,dy=worm[i+1].y-worm[i].y,dist=std::sqrt(dx*dx+dy*dy);double target=WORM_SEG_SIZE*0.85;if(dist>target){worm[i].x+=dx/dist*(dist-target);worm[i].y+=dy/dist*(dist-target);}}
        }

        if(eyesActive){int alive=0;for(int i=0;i<EYE_COUNT;i++){if(!eyes[i].alive)continue;alive++;eyes[i].x=bossX+std::cos(eyes[i].angle)*EYE_DIST;eyes[i].y=bossY+std::sin(eyes[i].angle)*EYE_DIST;}
            if(alive==0)eyesActive=false;else{eyeFireTimer-=dt;if(eyeFireTimer<=0.0){eyeFireTimer=EYE_FIRE_INTERVAL;
                for(int i=0;i<EYE_COUNT;i++){if(!eyes[i].alive)continue;double a=std::atan2(playerY-eyes[i].y,playerX-eyes[i].x);
                    for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_SULFUR;proj[j].x=eyes[i].x;proj[j].y=eyes[i].y;proj[j].vx=std::cos(a)*PROJ_SPEED;proj[j].vy=std::sin(a)*PROJ_SPEED;proj[j].timer=0.0;proj[j].accel=0.0;proj[j].active=true;break;}
                }}
            }}
        }

        if(brothersActive){broAttackTimer-=dt;
            if(!broAttacking){if(broA.alive){double tx=playerX+(broA.onLeft?-BRO_DISTANCE:BRO_DISTANCE);broA.x+=(tx-broA.x)*0.08;broA.y+=(playerY-broA.y)*0.08;}if(broB.alive){double tx=playerX+(broB.onLeft?-BRO_DISTANCE:BRO_DISTANCE);broB.x+=(tx-broB.x)*0.08;broB.y+=(playerY-broB.y)*0.08;}if(broAttackTimer<=0.0){broAttacking=true;broFistCount=0;broAttackTimer=-1.0;}}
            else if(broAttackTimer<0.0&&broFistCount<BRO_FIST_TOTAL){if(broA.alive){double a=std::atan2(playerY-broA.y,playerX-broA.x);for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=(broFistToggle==0)?PROJ_FIST1:PROJ_FIST2;proj[j].x=broA.x;proj[j].y=broA.y;proj[j].vx=std::cos(a)*WRAITH_INIT_SPD;proj[j].vy=std::sin(a)*WRAITH_INIT_SPD;proj[j].timer=0.0;proj[j].accel=WRAITH_ACCEL;proj[j].active=true;break;}broFistToggle=1-broFistToggle;}if(broB.alive){double a=std::atan2(playerY-broB.y,playerX-broB.x);bool isFast=(broFistCount%3==2);for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=(broBladeToggle==0)?PROJ_BLADE1:PROJ_BLADE2;proj[j].x=broB.x;proj[j].y=broB.y;proj[j].vx=std::cos(a)*(isFast?BLADE_FAST_SPEED:WRAITH_INIT_SPD);proj[j].vy=std::sin(a)*(isFast?BLADE_FAST_SPEED:WRAITH_INIT_SPD);proj[j].timer=0.0;proj[j].accel=isFast?0.0:WRAITH_ACCEL;proj[j].active=true;break;}broBladeToggle=1-broBladeToggle;}broFistCount++;if(broFistCount>=BRO_FIST_TOTAL)broAttackTimer=0.6;else{double t=(double)broFistCount/(BRO_FIST_TOTAL-1);broAttackTimer=BRO_FIST_INTV_START+(BRO_FIST_INTV_END-BRO_FIST_INTV_START)*t;}}
            if(broAttacking&&broFistCount>=BRO_FIST_TOTAL&&broAttackTimer<=0.0){broAttacking=false;broAttackTimer=2.0;bool aL=broA.onLeft;broA.onLeft=broB.onLeft;broB.onLeft=aL;}
        }

        if(invincibleTimer>0.0){invincibleTimer-=dt;playerVisible=(fmod(invincibleTimer*12.5,1.0)<0.5);if(invincibleTimer<=0.0){invincibleTimer=0.0;playerVisible=true;}}else playerVisible=true;

        if(bulletHellActive){bulletHellSpawnTimer-=dt;
            if(bulletHellSpawnTimer<=0.0){bulletHellSpawnTimer=BHELL_SPAWN_MIN+(rand()%1000)/1000.0*(BHELL_SPAWN_MAX-BHELL_SPAWN_MIN);
                int dir=rand()%4; double sx=0,sy=0,vx=0,vy=0;
                switch(dir){case 0:sx=playerX+(rand()%(int)(BHELL_SPREAD*2))-BHELL_SPREAD;sy=(double)(ARENA_T-30);vy=BHELL_SPEED;break;
                    case 1:sx=playerX+(rand()%(int)(BHELL_SPREAD*2))-BHELL_SPREAD;sy=(double)(ARENA_B+30);vy=-BHELL_SPEED;break;
                    case 2:sx=(double)(ARENA_L-30);sy=playerY+(rand()%(int)(BHELL_SPREAD*2))-BHELL_SPREAD;vx=BHELL_SPEED;break;
                    case 3:sx=(double)(ARENA_R+30);sy=playerY+(rand()%(int)(BHELL_SPREAD*2))-BHELL_SPREAD;vx=-BHELL_SPEED;break;}
                for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_BHELL;proj[j].x=sx;proj[j].y=sy;proj[j].vx=vx;proj[j].vy=vy;proj[j].timer=0.0;proj[j].accel=0.0;proj[j].active=true;break;}
            }
        }
        if(skullActive){skullSpawnTimer-=dt;if(skullSpawnTimer<=0.0){skullSpawnTimer=SKULL_SPAWN_MIN+(rand()%1000)/1000.0*(SKULL_SPAWN_MAX-SKULL_SPAWN_MIN);bool fR=(rand()%2)==0;double sx=fR?(double)(WIN_W+30):(double)(-30);double vx=fR?-SKULL_SPEED:SKULL_SPEED;double sy=(double)(ARENA_T+rand()%(int)(ARENA_B-ARENA_T));for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_SKULL;proj[j].x=sx;proj[j].y=sy;proj[j].baseY=sy;proj[j].vx=vx;proj[j].vy=0.0;proj[j].timer=(rand()%1000)/1000.0*SKULL_PERIOD;proj[j].accel=0.0;proj[j].active=true;break;}}}

        for(int i=0;i<MAX_PROJ;i++){if(!proj[i].active)continue;
            if(proj[i].type==PROJ_FISSION||proj[i].type==PROJ_ABYSS){proj[i].timer-=dt;bool isAb=(proj[i].type==PROJ_ABYSS);double sC=isAb?(double)ABYSS_SPLIT:(double)FISSION_SPLIT,tR=isAb?ABYSS_TURN:FISSION_TURN,tS=isAb?ABYSS_SPEED:FISSION_SPEED;
                if(proj[i].timer<=0.0){for(int k=0;k<(int)sC;k++){double a=k*2.0*PI/sC;for(int j=0;j<MAX_PROJ;j++)if(!proj[j].active){proj[j].type=PROJ_SULFUR;proj[j].x=proj[i].x;proj[j].y=proj[i].y;proj[j].vx=std::cos(a)*PROJ_SPEED;proj[j].vy=std::sin(a)*PROJ_SPEED;proj[j].timer=0.0;proj[j].accel=0.0;proj[j].active=true;break;}}proj[i].active=false;continue;}
                double dA=std::atan2(playerY-proj[i].y,playerX-proj[i].x),cA=std::atan2(proj[i].vy,proj[i].vx),diff=dA-cA;while(diff>PI)diff-=2*PI;while(diff<-PI)diff+=2*PI;double mT=tR*dt;if(diff>mT)diff=mT;else if(diff<-mT)diff=-mT;double nA=cA+diff;proj[i].vx=std::cos(nA)*tS;proj[i].vy=std::sin(nA)*tS;}
            if(proj[i].type==PROJ_WRAITH){double dir=(proj[i].vx>0)?1.0:-1.0;proj[i].vx+=dir*WRAITH_ACCEL*dt;}
            if(proj[i].type==PROJ_FIST1||proj[i].type==PROJ_FIST2){double dir=(proj[i].vx>0)?1.0:-1.0;proj[i].vx+=dir*WRAITH_ACCEL*dt;}
            if(proj[i].accel>0.0){double spd=std::sqrt(proj[i].vx*proj[i].vx+proj[i].vy*proj[i].vy);if(spd>0.1){proj[i].vx+=proj[i].vx/spd*proj[i].accel*dt;proj[i].vy+=proj[i].vy/spd*proj[i].accel*dt;}}
            proj[i].x+=proj[i].vx*dt;proj[i].y+=proj[i].vy*dt;
            if(proj[i].type==PROJ_SKULL){proj[i].timer+=dt;proj[i].y=proj[i].baseY+SKULL_AMP*sin(2.0*PI*proj[i].timer/SKULL_PERIOD);}

            if(proj[i].type==PROJ_FIRE){bool hit=false;double hw=PROJ_A_HB_W,hh=PROJ_A_HB_H;
                if(!(wormLen>0||eyesActive)&&rectCollide(proj[i].x,proj[i].y,hw,hh,bossX,bossY,(double)BOSS_W/3.0,(double)BOSS_H/3.0)){bossHP-=FIRE_DMG_BOSS;hit=true;}
                if(brothersActive){if(broA.alive&&rectCollide(proj[i].x,proj[i].y,hw,hh,broA.x,broA.y,30.0,30.0)){broA.hp-=FIRE_DMG_BRO;if(broA.hp<=0)broA.alive=false;hit=true;}if(broB.alive&&rectCollide(proj[i].x,proj[i].y,hw,hh,broB.x,broB.y,30.0,30.0)){broB.hp-=FIRE_DMG_BRO;if(broB.hp<=0)broB.alive=false;hit=true;}}
                for(int k=0;k<HEART_COUNT;k++)if(hearts[k].active&&rectCollide(proj[i].x,proj[i].y,hw,hh,hearts[k].x,hearts[k].y,(double)HEART_SIZE_W,(double)HEART_SIZE_H)){hearts[k].hp-=FIRE_DMG_SMALL;if(hearts[k].hp<=0)hearts[k].active=false;hit=true;}
                if(eyesActive)for(int k=0;k<EYE_COUNT;k++)if(eyes[k].alive&&rectCollide(proj[i].x,proj[i].y,hw,hh,eyes[k].x,eyes[k].y,(double)EYE_SIZE_W,(double)EYE_SIZE_H)){eyes[k].hp-=(int)FIRE_DMG_SMALL;if(eyes[k].hp<=0)eyes[k].alive=false;hit=true;}
                if(hit){proj[i].active=false;continue;}
            }
            if(brothersActive&&!broA.alive&&!broB.alive)brothersActive=false;
            if(eyesActive){bool any=false;for(int k=0;k<EYE_COUNT;k++)if(eyes[k].alive)any=true;if(!any)eyesActive=false;}

            if(proj[i].type!=PROJ_FIRE){double hbW=(proj[i].type==PROJ_BLADE1||proj[i].type==PROJ_BLADE2||proj[i].type==PROJ_FIST1||proj[i].type==PROJ_FIST2||proj[i].type==PROJ_BHELL||proj[i].type==PROJ_WRAITH)?PROJ_D_HB_W:(proj[i].type==PROJ_SKULL||proj[i].type==PROJ_ABYSS||proj[i].type==PROJ_FISSION)?((proj[i].type==PROJ_ABYSS)?PROJ_C_HB_W:PROJ_B_HB_W):PROJ_A_HB_W;double hbH=(proj[i].type==PROJ_BLADE1||proj[i].type==PROJ_BLADE2||proj[i].type==PROJ_FIST1||proj[i].type==PROJ_FIST2||proj[i].type==PROJ_BHELL||proj[i].type==PROJ_WRAITH)?PROJ_D_HB_H:(proj[i].type==PROJ_SKULL||proj[i].type==PROJ_ABYSS||proj[i].type==PROJ_FISSION)?((proj[i].type==PROJ_ABYSS)?PROJ_C_HB_H:PROJ_B_HB_H):PROJ_A_HB_H;
                if(!(invincibleTimer>0.0)&&rectCollide(proj[i].x,proj[i].y,hbW,hbH,playerX,playerY,PL_HB_W,PL_HB_H)){double d=DMG_SULFUR;if(proj[i].type==PROJ_FISSION||proj[i].type==PROJ_ABYSS)d=DMG_FISSION;else if(proj[i].type==PROJ_WRAITH||proj[i].type==PROJ_BHELL)d=DMG_WRAITH;else if(proj[i].type==PROJ_SKULL)d=DMG_SKULL;else if(proj[i].type>=PROJ_FIST1&&proj[i].type<=PROJ_BLADE2)d=DMG_FIST;dmgPlayer(d);invincibleTimer=INVINCIBLE_TIME;}}
            if(proj[i].x<-200||proj[i].x>WIN_W+200||proj[i].y<-200||proj[i].y>WIN_H+200)proj[i].active=false;
        }

        if(moonActive){double dx=playerX-moonX,dy=playerY-moonY,dist=std::sqrt(dx*dx+dy*dy);if(dist>1.0){moonX+=dx/dist*MOON_SPEED*dt;moonY+=dy/dist*MOON_SPEED*dt;}if(dist<MOON_COLLIDE_R&&moonDmgDelay<=0.0)dmgPlayer(DMG_MOON*dt);}

        cleardevice();
        setfillcolor(BLACK);solidrectangle(0,0,WIN_W,ARENA_T);solidrectangle(0,ARENA_B,WIN_W,WIN_H);solidrectangle(0,ARENA_T,ARENA_L,ARENA_B);solidrectangle(ARENA_R,ARENA_T,WIN_W,ARENA_B);
        setfillcolor(RGB(20,5,10));solidrectangle(ARENA_L,ARENA_T,ARENA_R,ARENA_B);
        if(moonActive){int mw=MOON_SIZE_W,mh=MOON_SIZE_H;putimageT_rotated((int)moonX,(int)moonY,mw,mh,&imgMoon,0.0);}
        if(wormLen>0){int sz=WORM_SEG_SIZE;for(int i=0;i<wormLen;i++){double a=(i==wormLen-1)?std::atan2(wormHeadVy,wormHeadVx)+PI/2.0:std::atan2(worm[i+1].y-worm[i].y,worm[i+1].x-worm[i].x)+PI/2.0;putimageT_rotated((int)worm[i].x,(int)worm[i].y,sz,sz,worm[i].img,a);}}
        int hwH=HEART_SIZE_W,hhH=HEART_SIZE_H;for(int i=0;i<HEART_COUNT;i++){if(!hearts[i].active)continue;putimageT_rotated((int)hearts[i].x,(int)hearts[i].y,hwH,hhH,&imgHeart,0.0);}
        if(brothersActive){Brother* brs[2]={&broA,&broB};for(int b=0;b<2;b++){if(!brs[b]->alive)continue;bool fr=(brs[b]->x>=playerX);IMAGE& ci=fr?*(brs[b]->imgRight):*(brs[b]->imgLeft);int bw=ci.getwidth()/2,bh=ci.getheight()/2;putimageT_rotated((int)brs[b]->x,(int)brs[b]->y,bw,bh,&ci,0.0);}}
        IMAGE& curBossImg=(bulletHellActive||f_end)?imgBWR:((wormLen>0||brothersActive||eyesActive)?(bossFacingRight?imgBWR:imgBWL):(bossFacingRight?imgBR:imgBL));
        int cbw=curBossImg.getwidth(),cbh=curBossImg.getheight();
        if(bossTransparent)putimageT_alpha((int)bossX-cbw/2,(int)bossY-cbh/2,&curBossImg,bossAlpha);else putimageT((int)bossX-cbw/2,(int)bossY-cbh/2,&curBossImg);
        for(int i=0;i<MAX_PROJ;i++){if(!proj[i].active)continue;double a=std::atan2(proj[i].vy,proj[i].vx)+PI/2.0;
            if(proj[i].type==PROJ_BLADE1){int pw=(int)PROJ_D_HB_W,ph=(int)PROJ_D_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgBd1,a);}
            else if(proj[i].type==PROJ_BLADE2){int pw=(int)PROJ_D_HB_W,ph=(int)PROJ_D_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgBd2,a);}
            else if(proj[i].type==PROJ_FIST1){int pw=(int)PROJ_D_HB_W,ph=(int)PROJ_D_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgF1,a);}
            else if(proj[i].type==PROJ_FIST2){int pw=(int)PROJ_D_HB_W,ph=(int)PROJ_D_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgF2,a);}
            else if(proj[i].type==PROJ_WRAITH||proj[i].type==PROJ_BHELL){int pw=(int)PROJ_D_HB_W,ph=(int)PROJ_D_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgPD,a);}
            else if(proj[i].type==PROJ_ABYSS){int pw=(int)PROJ_C_HB_W,ph=(int)PROJ_C_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgPC,a);}
            else if(proj[i].type==PROJ_FISSION){int pw=(int)PROJ_B_HB_W,ph=(int)PROJ_B_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgPB,a);}
            else if(proj[i].type==PROJ_FIRE){int pw=(int)PROJ_A_HB_W,ph=(int)PROJ_A_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgFire,a);}
            else if(proj[i].type==PROJ_SKULL){int pw=(int)PROJ_A_HB_W,ph=(int)PROJ_A_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgSk,a);}
            else{int pw=(int)PROJ_A_HB_W,ph=(int)PROJ_A_HB_H;putimageT_rotated((int)proj[i].x,(int)proj[i].y,pw,ph,&imgPA,a);}
        }
        if(eyesActive){int ew=EYE_SIZE_W,eh=EYE_SIZE_H;for(int i=0;i<EYE_COUNT;i++){if(!eyes[i].alive)continue;double a=std::atan2(playerY-eyes[i].y,playerX-eyes[i].x)+PI/2.0;putimageT_rotated((int)eyes[i].x,(int)eyes[i].y,ew,eh,&imgEye,a);}}
        if(playerVisible){IMAGE& pi=facingRight?imgPR:imgPL;putimageT((int)playerX-halfW,(int)playerY-halfH,&pi);}

        auto drawHPBar=[](int x,int y,int w,int h,double pct,COLORREF fill,COLORREF empty){setfillcolor(empty);solidrectangle(x,y,x+w,y+h);setfillcolor(fill);solidrectangle(x,y,x+(int)(w*pct/100.0),y+h);setlinecolor(WHITE);setlinestyle(PS_SOLID,1);rectangle(x,y,x+w,y+h);};
        {int bx=WIN_W-280,by=15;drawHPBar(bx,by,250,22,playerHP/playerMaxHP*100.0,RGB(0,200,0),RGB(40,40,40));char t[64];settextcolor(WHITE);settextstyle(16,0,_T("Consolas"));sprintf_s(t,"HP: %.0f/%.0f",playerHP,playerMaxHP);outtextxy(bx+5,by+3,t);if(potionCD>0.0){settextcolor(RGB(150,150,150));sprintf_s(t,"CD: %.0fs",potionCD);outtextxy(bx,by+25,t);}}
        {int bx=CX-200,by=ARENA_B+15;drawHPBar(bx,by,400,22,bossHP,RGB(200,40,40),RGB(40,40,40));char t[64];settextcolor(RGB(255,100,100));settextstyle(18,0,_T("Consolas"));sprintf_s(t,"Calamitas %.1f%%",bossHP);outtextxy(bx+5,by+3,t);}
        if(brothersActive){int bx=ARENA_R-200,by=ARENA_B+15;if(broA.alive){drawHPBar(bx,by,180,14,broA.hp,RGB(255,180,60),RGB(40,40,40));char t[64];settextcolor(RGB(255,200,100));settextstyle(14,0,_T("Consolas"));sprintf_s(t,"Calamity %.0f%%",broA.hp);outtextxy(bx+3,by,t);by+=20;}if(broB.alive){drawHPBar(bx,by,180,14,broB.hp,RGB(60,180,255),RGB(40,40,40));char t[64];settextcolor(RGB(100,200,255));settextstyle(14,0,_T("Consolas"));sprintf_s(t,"Disaster %.0f%%",broB.hp);outtextxy(bx+3,by,t);}}

        setlinecolor(RGB(120,20,30));setlinestyle(PS_SOLID,2);rectangle(ARENA_L,ARENA_T,ARENA_R,ARENA_B);
        FlushBatchDraw();
        int elapsed=(int)((clock()-frameStart)*1000/CLOCKS_PER_SEC);int remain=FRAME_MS-elapsed;if(remain>0)Sleep(remain);
    }
    EndBatchDraw();Gdiplus::GdiplusShutdown(gdpToken);closegraph();return 0; }}
