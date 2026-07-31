#include <easyx.h>
#include <ctime>
#include <cmath>
#include <cstdlib>
#include <cstdio>
#include <gdiplus.h>
#pragma comment(lib, "gdiplus.lib")
#include <mmsystem.h>
#include <shellapi.h>
#pragma comment(lib, "winmm.lib")

const double PI = 3.14159265358979323846;
inline int t2p(double t) { return (int)(t * 390.0 / 78.125); }

// Transparent draw
void putimageT(int x, int y, IMAGE* src) {
    int w = src->getwidth(), h = src->getheight();
    IMAGE mask(w, h), color(w, h);
    DWORD* pS = GetImageBuffer(src), * pM = GetImageBuffer(&mask), * pC = GetImageBuffer(&color);
    for (int i = 0; i < w * h; i++) {
        DWORD px = pS[i]; BYTE r = (px >> 16) & 0xFF, g = (px >> 8) & 0xFF, b = px & 0xFF;
        if (r < 20 && g < 20 && b < 20) { pM[i] = 0x00FFFFFF; pC[i] = 0x00000000; }
        else { pM[i] = 0x00000000; pC[i] = px; }
    }
    putimage(x, y, &mask, SRCAND); putimage(x, y, &color, SRCPAINT);
}

// GDI+ rotated draw (angle: 0=up, PI/2=right)

void putimageT_alpha(int x, int y, IMAGE* src, double alpha) {
    if (alpha >= 1.0) { putimageT(x, y, src);return; } if (alpha <= 0.0)return;
    int w = src->getwidth(), h = src->getheight();
    IMAGE dst(w, h); getimage(&dst, x, y, w, h);
    DWORD* pS = GetImageBuffer(src), * pD = GetImageBuffer(&dst);
    IMAGE out(w, h); DWORD* pO = GetImageBuffer(&out);
    for (int i = 0;i < w * h;i++) {
        DWORD sp = pS[i];
        if ((sp & 0x00FFFFFF) == 0) { pO[i] = pD[i]; }
        else {
            BYTE sr = (sp >> 16) & 0xFF, sg = (sp >> 8) & 0xFF, sb = sp & 0xFF, dr = (pD[i] >> 16) & 0xFF, dg = (pD[i] >> 8) & 0xFF, db = pD[i] & 0xFF;
            pO[i] = RGB((BYTE)(sr * alpha + dr * (1 - alpha)), (BYTE)(sg * alpha + dg * (1 - alpha)), (BYTE)(sb * alpha + db * (1 - alpha)));
        }
    }
    putimage(x, y, &out);
}
void putimageT_Rot(int cx, int cy, int dW, int dH, IMAGE* src, double angle) {
    int w = src->getwidth(), h = src->getheight();
    DWORD* pS = GetImageBuffer(src);
    Gdiplus::Bitmap bmp(w, h, PixelFormat32bppARGB);
    Gdiplus::BitmapData bd; Gdiplus::Rect rc(0, 0, w, h);
    bmp.LockBits(&rc, Gdiplus::ImageLockModeWrite, PixelFormat32bppARGB, &bd);
    BYTE* pD = (BYTE*)bd.Scan0;
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
        DWORD px = pS[y * w + x]; int idx = y * bd.Stride + x * 4;
        BYTE r = (px >> 16) & 0xFF, g = (px >> 8) & 0xFF, b = px & 0xFF;
        if ((r < 20 && g < 20 && b < 20) || (r > 235 && g > 235 && b > 235)) { pD[idx + 3] = 0; }
        else { pD[idx + 0] = b; pD[idx + 1] = g; pD[idx + 2] = r; pD[idx + 3] = 255; }
    }
    bmp.UnlockBits(&bd);
    Gdiplus::Graphics g(GetImageHDC(NULL));
    g.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBilinear);
    g.TranslateTransform((float)cx, (float)cy);
    g.RotateTransform((float)(angle * 180.0 / PI));
    g.ScaleTransform((float)dW / w, (float)dH / h);
    g.DrawImage(&bmp, -w / 2, -h / 2, w, h);
}

// Projectile system
const int MAX_PROJ = 256;

// 类型枚举 (对齐core.cpp)
enum ProjType {
    PROJ_SULFUR = 0, PROJ_FISSION = 1, PROJ_ABYSS = 2,
    PROJ_WRAITH = 3, PROJ_BHELL = 4, PROJ_FIST1 = 5,
    PROJ_FIST2 = 6, PROJ_BLADE1 = 7, PROJ_BLADE2 = 8,
    PROJ_SKULL = 9, PROJ_FIRE = 10
};

// 基类
struct Projectile {
    bool   active = false;
    ProjType type = PROJ_SULFUR;
    double x = 0, y = 0, vx = 0, vy = 0;
    double timer = 0, accel = 0, baseY = 0;

    virtual ~Projectile() {}
    virtual void update(double dt) = 0;
    virtual void draw() = 0;
    virtual double hbW() = 0;   // 碰撞箱半宽
    virtual double hbH() = 0;
};

// 全局弹幕池 (对齐core.cpp: Projectile proj[MAX_PROJ])
Projectile* proj[MAX_PROJ] = {};

bool rectCollide(double x1, double y1, double w1, double h1, double x2, double y2, double w2, double h2) {
    return (x1 - w1 / 2) < (x2 + w2 / 2) && (x1 + w1 / 2) > (x2 - w2 / 2) && (y1 - h1 / 2) < (y2 + h2 / 2) && (y1 + h1 / 2) > (y2 - h2 / 2);
}

// Player Fire
class FireProj : public Projectile {
    static IMAGE img; static double _hbW, _hbH;
public:
    static void loadImage() { loadimage(&img, _T("assets\\fire.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    static IMAGE& getImg() { return img; }
    FireProj(double sx, double sy, double angle) {
        active = true; type = PROJ_FIRE; x = sx; y = sy; timer = 0; accel = 0; baseY = 0;
        const double SPD = 300.0 * 390.0 / 78.125;
        vx = std::cos(angle) * SPD; vy = std::sin(angle) * SPD;
    }
    void update(double dt) override { x += vx * dt; y += vy * dt; if (x < -200 || x>1700 || y < -200 || y>1200) active = false; }
    void draw() override { int sz = (int)_hbW; putimageT_Rot((int)x, (int)y, sz, sz, &img, std::atan2(vy, vx) + PI / 2.0); }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE FireProj::img; double FireProj::_hbW = 0, FireProj::_hbH = 0;

void spawnFire(double sx, double sy, double tx, double ty) {
    double a = std::atan2(ty - sy, tx - sx);
    for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) { delete proj[i]; proj[i] = new FireProj(sx, sy, a); return; }
}

// Sulfur Fireball
class SulfurProj : public Projectile {
    static IMAGE img;
    static double _hbW, _hbH;
    double speed;
public:
    static void loadImage() { loadimage(&img, _T("assets\\a.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    static double getHBW() { return _hbW; }
    static double getHBH() { return _hbH; }
    static IMAGE& getImg() { return img; }

    SulfurProj(double sx, double sy, double angle, double spd)
        : speed(spd) {
        active = true; type = PROJ_SULFUR; x = sx; y = sy;
        vx = std::cos(angle) * speed;
        vy = std::sin(angle) * speed;
        timer = 0.0; accel = 0.0; baseY = 0.0;
    }
    void update(double dt) override {
        x += vx * dt; y += vy * dt;
        // out of bounds
        if (x < -200 || x>1700 || y < -200 || y>1200) active = false;
    }
    void draw() override {
        double a = std::atan2(vy, vx) + PI / 2.0;
        int szW = (int)_hbW, szH = (int)_hbH;
        putimageT_Rot((int)x, (int)y, szW, szH, &img, a);
    }
    double hbW() override { return _hbW; }
    double hbH() override { return _hbH; }
};
IMAGE  SulfurProj::img;
double SulfurProj::_hbW = 0, SulfurProj::_hbH = 0;

// Wraith
class WraithProj : public Projectile {
    static IMAGE img; static double _hbW, _hbH; double accR;
public:
    static void loadImage() { loadimage(&img, _T("assets\\d.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    static IMAGE& getImg() { return img; }
    WraithProj(double sx, double sy, double dir, double spd, double acc) {
        active = true; type = PROJ_WRAITH; x = sx; y = sy; timer = 0; accel = acc; baseY = 0;
        vx = dir * spd; vy = 0; accR = acc;
    }
    void update(double dt) override {
        double d = (vx > 0) ? 1.0 : -1.0; vx += d * accR * dt;
        x += vx * dt; y += vy * dt;
        if (x < -300 || x>1800 || y < -300 || y>1300) active = false;
    }
    void draw() override {
        int w = (int)_hbW, h = (int)_hbH;
        putimageT_Rot((int)x, (int)y, w, h, &img, std::atan2(vy, vx) + PI / 2.0);
    }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE WraithProj::img; double WraithProj::_hbW = 0, WraithProj::_hbH = 0;

// Fission
class FissionProj : public Projectile {
    static IMAGE img; static double _hbW, _hbH; double trnR, spd, * px, * py;
public:
    static void loadImage() { loadimage(&img, _T("assets\\b.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    static IMAGE& getImg() { return img; }
    FissionProj(double sx, double sy, double angle, double speed, double turn, double* ppx, double* ppy) {
        active = true; type = PROJ_FISSION; x = sx; y = sy; timer = 2.0; accel = 0; baseY = 0; trnR = turn; spd = speed;
        vx = std::cos(angle) * speed; vy = std::sin(angle) * speed; px = ppx; py = ppy;
    }
    void update(double dt) override {
        timer -= dt;
        if (timer <= 0.0) {
            for (int k = 0;k < 16;k++) {
                double a = k * 2.0 * PI / 16;
                for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                    delete proj[i]; proj[i] = new SulfurProj(x, y, a, 37.5 * 390.0 / 78.125); break;
                }
            }
            active = false; return;
        }
        double dA = std::atan2(*py - y, *px - x), cA = std::atan2(vy, vx), diff = dA - cA;
        while (diff > PI)diff -= 2 * PI; while (diff < -PI)diff += 2 * PI;
        double mT = trnR * dt; if (diff > mT)diff = mT; else if (diff < -mT)diff = -mT;
        double nA = cA + diff; vx = std::cos(nA) * spd; vy = std::sin(nA) * spd;
        x += vx * dt; y += vy * dt;
    }
    void draw() override {
        int w = (int)_hbW, h = (int)_hbH;
        putimageT_Rot((int)x, (int)y, w, h, &img, std::atan2(vy, vx) + PI / 2.0);
    }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE FissionProj::img; double FissionProj::_hbW = 0, FissionProj::_hbH = 0;

// Abyss
class AbyssProj : public Projectile {
    static IMAGE img; static double _hbW, _hbH; double trnR, spd, * px, * py;
public:
    static void loadImage() { loadimage(&img, _T("assets\\c.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    AbyssProj(double sx, double sy, double angle, double speed, double turn, double* ppx, double* ppy) {
        active = true; type = PROJ_ABYSS; x = sx; y = sy; timer = 2.5; accel = 0; baseY = 0; trnR = turn; spd = speed;
        vx = std::cos(angle) * speed; vy = std::sin(angle) * speed; px = ppx; py = ppy;
    }
    void update(double dt) override {
        timer -= dt;
        if (timer <= 0.0) { for (int k = 0;k < 36;k++) { double a = k * 2.0 * PI / 36; for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) { delete proj[i]; proj[i] = new SulfurProj(x, y, a, 37.5 * 390.0 / 78.125); break; } } active = false; return; }
        double dA = std::atan2(*py - y, *px - x), cA = std::atan2(vy, vx), diff = dA - cA;
        while (diff > PI)diff -= 2 * PI; while (diff < -PI)diff += 2 * PI; double mT = trnR * dt;
        if (diff > mT)diff = mT; else if (diff < -mT)diff = -mT; double nA = cA + diff;
        vx = std::cos(nA) * spd; vy = std::sin(nA) * spd; x += vx * dt; y += vy * dt;
    }
    void draw() override { int w = (int)_hbW, h = (int)_hbH; putimageT_Rot((int)x, (int)y, w, h, &img, std::atan2(vy, vx) + PI / 2.0); }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE AbyssProj::img; double AbyssProj::_hbW = 0, AbyssProj::_hbH = 0;

// Skull
class SkullProj : public Projectile {
    static IMAGE img; static double _hbW, _hbH; double baseY, amp, period, phase;
public:
    static void loadImage() { loadimage(&img, _T("assets\\e.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    SkullProj(double sx, double sy, double spd, bool toRight, double amplitude, double per) {
        active = true; type = PROJ_SKULL; x = sx; y = sy; timer = 0; accel = 0; baseY = sy; amp = amplitude; period = per;
        phase = (rand() % 1000) / 1000.0 * period; vx = toRight ? spd : -spd; vy = 0;
    }
    void update(double dt) override {
        timer += dt; x += vx * dt;
        y = baseY + amp * std::sin(2.0 * PI * timer / period);
        if (x < -300 || x>1800 || y < -300 || y>1300) active = false;
    }
    void draw() override { int w = (int)_hbW, h = (int)_hbH; putimageT_Rot((int)x, (int)y, w, h, &img, std::atan2(vy, vx) + PI / 2.0); }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE SkullProj::img; double SkullProj::_hbW = 0, SkullProj::_hbH = 0;

// Fist (brother A)
class FistProj : public Projectile {
    static IMAGE img1, img2; static double _hbW, _hbH; double accR; int which;
public:
    static void loadImage() { loadimage(&img1, _T("assets\\a1.png")); loadimage(&img2, _T("assets\\a2.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    FistProj(double sx, double sy, double angle, double spd, double acc, int w) {
        active = true; type = (w == 0) ? PROJ_FIST1 : PROJ_FIST2; x = sx;y = sy;timer = 0;accel = acc;baseY = 0;
        vx = std::cos(angle) * spd; vy = std::sin(angle) * spd; accR = acc; which = w;
    }
    void update(double dt) override { double s = std::sqrt(vx * vx + vy * vy); if (s > 0.1) { vx += vx / s * accR * dt;vy += vy / s * accR * dt; } x += vx * dt;y += vy * dt; if (x < -300 || x>1800 || y < -300 || y>1300)active = false; }
    void draw() override { IMAGE& im = (which == 0) ? img1 : img2; int sz = (int)_hbW; putimageT_Rot((int)x, (int)y, sz, sz, &im, std::atan2(vy, vx) + PI / 2.0); }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE FistProj::img1, FistProj::img2; double FistProj::_hbW = 0, FistProj::_hbH = 0;

// Blade (brother B)
class BladeProj : public Projectile {
    static IMAGE img1, img2; static double _hbW, _hbH; int which;
public:
    static void loadImage() { loadimage(&img1, _T("assets\\b1.png")); loadimage(&img2, _T("assets\\b2.png")); }
    static void setHB(double w, double h) { _hbW = w; _hbH = h; }
    BladeProj(double sx, double sy, double angle, double spd, double acc, int w) {
        active = true; type = (w == 0) ? PROJ_BLADE1 : PROJ_BLADE2; x = sx;y = sy;timer = 0;accel = acc;baseY = 0;
        vx = std::cos(angle) * spd; vy = std::sin(angle) * spd; which = w;
    }
    void update(double dt) override { if (accel > 0) { double s = std::sqrt(vx * vx + vy * vy);if (s > 0.1) { vx += vx / s * accel * dt;vy += vy / s * accel * dt; } } x += vx * dt;y += vy * dt; if (x < -300 || x>1800 || y < -300 || y>1300)active = false; }
    void draw() override { IMAGE& im = (which == 0) ? img1 : img2; int sz = (int)_hbW; putimageT_Rot((int)x, (int)y, sz, sz, &im, std::atan2(vy, vx) + PI / 2.0); }
    double hbW() override { return _hbW; } double hbH() override { return _hbH; }
};
IMAGE BladeProj::img1, BladeProj::img2; double BladeProj::_hbW = 0, BladeProj::_hbH = 0;

// Projectile Manager
void clearProjectiles() { for (int i = 0;i < MAX_PROJ;i++) { delete proj[i]; proj[i] = nullptr; } }

void updateProjectiles(double dt) {
    for (int i = 0;i < MAX_PROJ;i++) if (proj[i] && proj[i]->active) {
        proj[i]->update(dt);
        if (!proj[i]->active) { delete proj[i]; proj[i] = nullptr; }
    }
}
void drawProjectiles() {
    for (int i = 0;i < MAX_PROJ;i++) if (proj[i] && proj[i]->active) proj[i]->draw();
}

// ==================== main ====================
int main()
{
    const int WIN_W = 1500, WIN_H = 1000;
    initgraph(WIN_W, WIN_H);

    const int CX = WIN_W / 2, CY = WIN_H / 2;
    const int ARENA_HALF = 390;
    const int ARENA_L = CX - ARENA_HALF, ARENA_T = CY - ARENA_HALF;
    const int ARENA_R = CX + ARENA_HALF, ARENA_B = CY + ARENA_HALF;

    // Background music (loop via MCI)
    // Intro SFX first, then BGM loop
    BeginBatchDraw();
    sndPlaySound(L"assets/1.wav", SND_FILENAME | SND_ASYNC);
    // Intro text loop
    { double it=0.0; clock_t ic=clock();
    while(it<4.0){ clock_t fc=clock(); double dt=(double)(fc-ic)/CLOCKS_PER_SEC; ic=fc; if(dt>0.1)dt=1.0/60.0; it+=dt;
        cleardevice(); setfillcolor(BLACK); solidrectangle(0,0,WIN_W,WIN_H);
        settextcolor(RGB(255,165,0)); settextstyle(36,0,L"Consolas");
        const wchar_t* m=L"Are You Ready For Hell?"; outtextxy(CX-textwidth(m)/2,CY-20,m);
        FlushBatchDraw(); Sleep(16);
    }}
    EndBatchDraw();
    sndPlaySound(L"assets/music.wav", SND_FILENAME | SND_ASYNC | SND_LOOP);

    Gdiplus::GdiplusStartupInput gpsi; ULONG_PTR gdptok;
    Gdiplus::GdiplusStartup(&gdptok, &gpsi, NULL);


    // 加载贴图
    IMAGE imgBR, imgBL, imgBWR, imgBWL, imgPR, imgPL;
    loadimage(&imgBR, _T("assets\\zaieright.png")); loadimage(&imgBL, _T("assets\\zaieleft.png"));
    loadimage(&imgBWR, _T("assets\\wudiright.png")); loadimage(&imgBWL, _T("assets\\wudileft.png"));
    loadimage(&imgPR, _T("assets\\playerright.png")); loadimage(&imgPL, _T("assets\\playerleft.png"));

    // 弹幕贴图+尺寸
    SulfurProj::loadImage();
    int sw = SulfurProj::getImg().getwidth(), sh = SulfurProj::getImg().getheight();
    SulfurProj::setHB(sw * 0.35, sh * 0.35);
    FireProj::loadImage();
    FireProj::setHB(sw * 0.35, sh * 0.35);
    WraithProj::loadImage();
    double whb = sw * 0.35 * 1.5 * 2.0;
    WraithProj::setHB(whb, whb);
    FissionProj::loadImage();
    FissionProj::setHB(sw * 0.35, sh * 0.35);
    AbyssProj::loadImage();
    AbyssProj::setHB(sw * 0.35 * 3.0, sh * 0.35 * 3.0); SkullProj::loadImage(); SkullProj::setHB(sw * 0.35, sh * 0.35);
    FistProj::loadImage(); BladeProj::loadImage();
    FistProj::setHB(whb, whb); BladeProj::setHB(whb, whb);  // same as wraith

    // ===== Boss State =====
    enum BossState {
        NORMAL, SKILL2_PREP, SKILL2_BURST, SKILL3, SKILL4,
        DASH_WINDUP, DASH, DASH_PAUSE
    };
    BossState bossState = NORMAL;

    // ===== Boss 常态追踪 =====
    double bossX = CX, bossY = CY, bossTX = CX, bossTY = CY; bool bossFR = true;
    int bossPhase = 0; double bossPhaseTimer = 0.0;
    const double BOSS_PHASE_DUR = 2.0, BOSS_RESP = 5.0;
    const int BOSS_ABOVE_Y = t2p(44), BOSS_SIDE_X = t2p(39);

    // ===== Attack Pattern =====
    enum PatternSkill { PSK_SULFUR, PSK_WRAITH, PSK_ABYSS, PSK_DASH };
    struct PtnStep { PatternSkill skill;int count; };
    const PtnStep PATTERN[] = {
        {PSK_SULFUR,-1},{PSK_WRAITH,-1},{PSK_ABYSS,2},{PSK_DASH,4},{PSK_ABYSS,2},{PSK_WRAITH,-1},{PSK_SULFUR,-1},
        {PSK_DASH,2},{PSK_SULFUR,-1},{PSK_WRAITH,-1},{PSK_ABYSS,4},{PSK_WRAITH,-1},{PSK_DASH,2},{PSK_SULFUR,-1},
        {PSK_ABYSS,4},{PSK_DASH,4},{PSK_SULFUR,-1},{PSK_DASH,2},{PSK_SULFUR,-1},{PSK_DASH,2},{PSK_SULFUR,-1}
    };
    const int PATTERN_LEN = sizeof(PATTERN) / sizeof(PATTERN[0]);
    int patternIndex = 0; double patternCooldown = 1.5;
    bool phase2 = false, phase2Opening = false, bossWeak = false;
    const double BOSS_MAX_HP = 100.0; double bossHP = BOSS_MAX_HP;
    bool f_bh1 = false, f_w1 = false, f_w1d = false, f_bh2 = false, f_bh3 = false;
    bool f_bro = false, f_broD = false, f_p2 = false, f_bh4 = false, f_eye = false, f_eyeD = false;
    bool f_bh5 = false, f_w2 = false, f_w2d = false, f_end = false;

    // ===== Bullet Hell =====
    bool bulletHellActive = false; int bhOccurrence = 0; double bhSpecTimer = 0.0, bhSpecInterval = 3.5; bool bhSpecToggle = false, bhSpecAlt = false, bhSkulls = false; double bhTimer = 15.0, bhSpawnCD = 0.0;
    const double BH_SPD = 20.0 * 390.0 / 78.125;
    const double BH_SPAWN_MIN = 0.09375, BH_SPAWN_MAX = 0.16875;
    const int BH_SPREAD = t2p(18);

    // ===== Skull Spawner =====
    bool skullActive = false; double skullCD = 0.0;
    const double SKULL_SPD = 20.0 * 390.0 / 78.125;
    const double SKULL_CD_MIN = BH_SPAWN_MIN * 4.0;
    const double SKULL_CD_MAX = BH_SPAWN_MAX * 4.0;

    // ===== Worm =====
    const int WORM_SEG_SZ = t2p(9), WORM_MAX = 40;
    struct WormSeg { double x, y; IMAGE* img; };
    WormSeg worm[WORM_MAX]; int wormLen = 0;
    IMAGE imgWH, imgWBA, imgWBB, imgWT;
    loadimage(&imgWH, _T("assets\\head.png")); loadimage(&imgWBA, _T("assets\\bodya.png"));
    loadimage(&imgWBB, _T("assets\\bodyb.png")); loadimage(&imgWT, _T("assets\\tail.png"));
    enum WormSt { WORM_RAND, WORM_TRACK }; WormSt wormSt = WORM_RAND;
    double wormStT = 4.0, wormTurnT = 0.0, wormHVx = 0.0, wormHVy = 0.0;
    const double WORM_SPD = 45.0 * 390.0 / 78.125, WORM_TURN = 0.6;
    const double WORM_RING_ACC = 10.0 * 390.0 / 78.125;
    auto initWorm = [&]() { if (wormLen == 0)return;
    for (int i = 0;i < wormLen;i++) {
        if (i == wormLen - 1)worm[i].img = &imgWH; else if (i == 0)worm[i].img = &imgWT;
        else if (i % 2 == 0)worm[i].img = &imgWBA; else worm[i].img = &imgWBB;
        worm[i].x = (double)(ARENA_L + 100 + i * WORM_SEG_SZ); worm[i].y = (double)(ARENA_T + 80);
    }
        };
    wormLen = 0;  // disabled

    // ===== Red Moon =====
    bool moonActive = false;
    IMAGE imgMoon; loadimage(&imgMoon, _T("assets\\moon.png"));
    const double MOON_SCALE = t2p(60) / 824.0;  // 824px diameter -> 60 tiles
    const int MOON_W = (int)(imgMoon.getwidth() * MOON_SCALE), MOON_H = (int)(imgMoon.getheight() * MOON_SCALE);
    const double MOON_R = t2p(30);  // collision radius
    const double MOON_SPD = 12.0 * 390.0 / 78.125;
    double moonX = CX, moonY = CY, moonDmgDelay = 2.5;

    // ===== Eyes =====
    bool eyesActive = false;
    const int EYE_COUNT = 10;
    const int EYE_SIZE = (int)(sw * 0.35 * 3.0);  // same as abyss
    const double EYE_DIST = t2p(12);
    struct Eye { double x, y, angle; int hp; bool alive; };
    Eye eyes[EYE_COUNT];
    IMAGE imgEye; loadimage(&imgEye, _T("assets\\eye.png"));
    double eyeFireCD = 0.0;
    for (int i = 0;i < EYE_COUNT;i++) { eyes[i].angle = i * 2.0 * PI / EYE_COUNT; eyes[i].hp = 100; eyes[i].alive = true; }

    // ===== Hearts =====
    const int HEART_COUNT = 10;
    struct Heart { double x, y; bool active; double hp; };
    Heart hearts[HEART_COUNT];
    IMAGE imgHeart; loadimage(&imgHeart, _T("assets\\heart.png"));
    const int HEART_SIZE_W = (int)(sw * 0.35 * 3.0), HEART_SIZE_H = (int)(sh * 0.35 * 2.5);
    const int HEART_DX = t2p(15), HEART_DY = t2p(3);
    const double HEART_APEX = (double)(ARENA_T + 80);
    for (int i = 0;i < HEART_COUNT;i++) {
        int side = (i < 5) ? -1 : 1, idx = (i < 5) ? (4 - i) : (i - 5);
        hearts[i].x = (double)CX + side * HEART_DX * (idx + 0.5);
        hearts[i].y = HEART_APEX + HEART_DY * (idx + 0.5); hearts[i].active = false; hearts[i].hp = 100;
    }

    // ===== Brothers =====
    IMAGE imgBrAL, imgBrAR, imgBrBL, imgBrBR;
    loadimage(&imgBrAL, _T("assets\\aleft.png")); loadimage(&imgBrAR, _T("assets\\aright.png"));
    loadimage(&imgBrBL, _T("assets\\bleft.png")); loadimage(&imgBrBR, _T("assets\\bright.png"));
    struct Brother { double x, y; bool alive, onLeft; double hp; IMAGE* imgL, * imgR; };
    Brother broA = { 0,0,true,false,100.0,&imgBrAL,&imgBrAR }; // Calamity
    Brother broB = { 0,0,true,true,100.0,&imgBrBL,&imgBrBR };  // Disaster
    bool brothersActive = false;
    const double BRO_DIST = t2p(70), BRO_SPAWN = t2p(50);

    // ===== Skill 2: 硫火飞弹 =====
    int    skill2BurstsTotal = 0, skill2BurstsRemaining = 0;
    double skill2Timer = 0.0;
    const double SKILL2_PREP_TIME = 0.4;
    const double SKILL2_BURST_INTERVAL = 2.0;
    double skill2CurInterval = 2.0;
    const double SKILL2_SPD = 37.5 * 390.0 / 78.125;
    const int    SKILL2_PER_BURST = 8;
    const double SKILL2_FAN = PI * 80.0 / 180.0;
    double skill2CD = 3.0;

    // ===== Skill 3: 深渊炙炎 =====
    int skill3BurstsTotal = 0, skill3BurstsRemaining = 0; double skill3Timer = 0;
    const double SKILL3_SPD = 20.0 * 390.0 / 78.125, SKILL3_TURN = 2.5;
    const double ABYSS_BURST_INTERVAL = 2.0;  // burst interval

    // ===== Skill 4: 深渊亡魂 =====
    int    skill4BurstsTotal = 0, skill4BurstsRemaining = 0;
    double skill4Timer = 0.0;
    const double SKILL4_SPD = 40.0 * 390.0 / 78.125;
    const double SKILL4_ACC = 140.0 * 390.0 / 78.125;
    const int    SKILL4_SIDE_X = t2p(55);  // 55 tiles from player
    double skill4CD = 1.5;

    // Dash
    int dashTotal = 0, dashRemain = 0;
    double dashTimer = 0, dashAngle = 0, dashDist = 0, dashTraveled = 0;
    double dashTX = 0, dashTY = 0; bool dashHalf = false;
    const double DASH_SPD = 200.0 * 390.0 / 78.125;
    const double WINDUP_TIME = 0.5, PAUSE_TIME = 0.2;
    const double TRANSPARENT_ALPHA = 0.35;
    bool bossTransparent = false; double bossAlpha = 1.0;
    double dashCD = 4.0;

    // ===== Player =====
    double playerX = CX, playerY = ARENA_B - 80; bool playerFR = true;
    const double PLAYER_SPEED = 61.0 * 390.0 / 78.125;
    const double PL_HB_W = t2p(2), PL_HB_H = t2p(3);
    // Damage+HP
    const double DMG_SULFUR = 60, DMG_FISSION = 110, DMG_DASH = 130, DMG_WRAITH = 90;
    const double DMG_SKULL = 90, DMG_FIST = 75, DMG_MOON = 50;
    const double PL_HP_NORMAL = 500.0; double playerHP = PL_HP_NORMAL, playerMaxHP = PL_HP_NORMAL;
    auto dmgPlayer = [&](double dmg) {if (playerHP > 0) { playerHP -= dmg;if (playerHP < 0)playerHP = 0; }};
    const double INVINCIBLE_TIME = 0.8; double invincibleTimer = 0.0; bool playerVisible = true;
    const double PL_HP_REGEN = 5.0; double potionCD = 0.0;
    // Brother init (after playerX/Y defined)
    broB.x = playerX - BRO_SPAWN; broB.y = playerY; broB.onLeft = true;
    broA.x = playerX + BRO_SPAWN; broA.y = playerY; broA.onLeft = false;
    // Brother attack
    double broAttackTimer = 2.0; int broFistCount = 0, broFistToggle = 0, broBladeToggle = 0; bool broAttacking = false;
    const int BRO_FIST_TOTAL = 18; const double BRO_FIST_INTV_START = 0.7, BRO_FIST_INTV_END = 0.3;
    const double BRO_FIST_SPD = 40.0 * 390.0 / 78.125, BRO_FIST_ACC = 140.0 * 390.0 / 78.125, BLADE_FAST_SPEED = 100.0 * 390.0 / 78.125;

    clock_t lastClock = clock(); const double FRAME_DT = 1.0 / 60.0;
    srand((unsigned)time(NULL));

    BeginBatchDraw();
    while (!(GetAsyncKeyState(VK_ESCAPE) & 0x8000)) {
        clock_t fs = clock(); double dt = (double)(fs - lastClock) / CLOCKS_PER_SEC;
        lastClock = fs; if (dt > 0.1)dt = FRAME_DT;

        // Phase monitor — run when game starts
        {
            static bool pmOnce = false; if (!pmOnce) { pmOnce = true;bhOccurrence = 1;bulletHellActive = true;bhTimer = 15.0;bhSkulls = false; }
            if (bulletHellActive) {
                bhTimer -= dt; if (bhTimer <= 0.0) {
                    bulletHellActive = false; bhTimer = -1.0;
                    if (bhSkulls) { bhSkulls = false;skullActive = false; }
                    for (int i = 0;i < MAX_PROJ;i++) if (proj[i] && proj[i]->active && (proj[i]->type == PROJ_BHELL || proj[i]->type == PROJ_SKULL)) proj[i]->active = false;
                }
            }
            if (!bulletHellActive && !f_w1) { f_w1 = true;bossWeak = true;wormLen = 16;initWorm();for (int i = 0;i < HEART_COUNT;i++) { hearts[i].active = true;hearts[i].hp = 100; } }
            if (f_w1 && !f_w1d) { bool ad = true;for (int i = 0;i < HEART_COUNT;i++)if (hearts[i].active)ad = false;if (ad) { f_w1d = true;wormLen = 0;bossWeak = false; } }
            if (!f_bh2 && f_w1d && bossHP < 75.0) { f_bh2 = true;bhOccurrence = 2;bulletHellActive = true;bhTimer = 15.0;bhSpecInterval = 3.5;bhSpecAlt = false;bhSpecTimer = 0.0;bhSkulls = false; }
            if (!f_bh3 && f_bh2 && !bulletHellActive && bossHP < 50.0) { f_bh3 = true;bhOccurrence = 3;bulletHellActive = true;bhTimer = 15.0;bhSpecInterval = 2.5;bhSpecAlt = true;bhSpecToggle = false;bhSpecTimer = 0.0;bhSkulls = false; }
            if (!f_bro && f_bh3 && !bulletHellActive && bossHP < 45.0) { f_bro = true;brothersActive = true; }
            if (f_bro && !f_broD && !brothersActive) { f_broD = true;f_p2 = true;phase2 = true;phase2Opening = true; }
            if (!f_bh4 && f_p2 && bossHP < 30.0) { f_bh4 = true;bhOccurrence = 4;bulletHellActive = true;bhTimer = 15.0;bhSpecInterval = 2.5;bhSpecAlt = true;bhSpecToggle = false;bhSpecTimer = 0.0;bhSkulls = false;moonActive = true;moonDmgDelay = 2.5;moonX = CX;moonY = CY; }
            if (!f_eye && f_bh4 && !bulletHellActive && bossHP < 20.0) { f_eye = true;eyesActive = true; }
            if (f_eye && !f_eyeD && !eyesActive) { f_eyeD = true; }
            if (!f_bh5 && f_eyeD && bossHP < 10.0) { f_bh5 = true;bhOccurrence = 5;bulletHellActive = true;bhTimer = 15.0;bhSpecInterval = 3.5;bhSpecAlt = true;bhSpecToggle = false;bhSpecTimer = 0.0;bhSkulls = true;skullActive = true; }
            if (!f_w2 && f_bh5 && !bulletHellActive && bossHP < 8.0) { f_w2 = true;bossWeak = true;wormLen = 16;initWorm();for (int i = 0;i < HEART_COUNT;i++) { hearts[i].active = true;hearts[i].hp = 100; } }
            if (f_w2 && !f_w2d) { bool ad = true;for (int i = 0;i < HEART_COUNT;i++)if (hearts[i].active)ad = false;if (ad) { f_w2d = true;wormLen = 0;bossWeak = false; } }
            if (!f_end && bossHP < 1.0) { f_end = true;bulletHellActive = false;moonActive = false;brothersActive = false;eyesActive = false;bhSkulls = false;for (int i = 0;i < MAX_PROJ;i++)if (proj[i])proj[i]->active = false;bossX = CX;bossY = CY; }
            if (moonActive && moonDmgDelay > 0.0) moonDmgDelay -= dt;
        }

        // BH special spawns (fission/abyss during BH#2-#5)
        if (bulletHellActive && bhOccurrence >= 2) {
            bhSpecTimer -= dt;
            if (bhSpecTimer <= 0.0) {
                bhSpecTimer = bhSpecInterval;
                ProjType sp = bhSpecAlt ? (bhSpecToggle ? PROJ_ABYSS : PROJ_FISSION) : PROJ_FISSION;
                if (bhSpecAlt) bhSpecToggle = !bhSpecToggle;
                double a = std::atan2(playerY - bossY, playerX - bossX);
                double spd = (sp == PROJ_ABYSS) ? (20.0 * 390.0 / 78.125) : (30.0 * 390.0 / 78.125);
                for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                    delete proj[i];
                    if (sp == PROJ_ABYSS) proj[i] = new AbyssProj(bossX, bossY, a, spd, 2.5, &playerX, &playerY);
                    else proj[i] = new FissionProj(bossX, bossY, a, spd, 3.0, &playerX, &playerY);
                    break;
                }
            }
        }

        // BH normal spawn
        {
            static double bhCD = 0.0; if (bulletHellActive) {
                bhCD -= dt;
                if (bhCD <= 0.0) {
                    bhCD = BH_SPAWN_MIN + (rand() % 1000) / 1000.0 * (BH_SPAWN_MAX - BH_SPAWN_MIN);
                    int dir = rand() % 4; double sx = 0, sy = 0, vx = 0, vy = 0;
                    switch (dir) {
                    case 0:sx = playerX + (rand() % (360)) - 180;sy = (double)(ARENA_T - 30);vy = BH_SPD;break;
                    case 1:sx = playerX + (rand() % (360)) - 180;sy = (double)(ARENA_B + 30);vy = -BH_SPD;break;
                    case 2:sx = (double)(ARENA_L - 30);sy = playerY + (rand() % (360)) - 180;vx = BH_SPD;break;
                    case 3:sx = (double)(ARENA_R + 30);sy = playerY + (rand() % (360)) - 180;vx = -BH_SPD;break;
                    }
                    for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                        delete proj[i]; proj[i] = new WraithProj(sx, sy, 1, BH_SPD, 0); proj[i]->type = PROJ_BHELL; proj[i]->vx = vx; proj[i]->vy = vy; break;
                    }
                }
            }
        }

        // ----- 玩家移动 -----
        bool L = (GetAsyncKeyState('A') & 0x8000) != 0, R = (GetAsyncKeyState('D') & 0x8000) != 0;
        bool U = (GetAsyncKeyState('W') & 0x8000) != 0 || (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
        bool D = (GetAsyncKeyState('S') & 0x8000) != 0;
        int step = (int)(PLAYER_SPEED * dt + 0.5);
        if (L)playerX -= step; if (R)playerX += step; if (U)playerY -= step; if (D)playerY += step;
        if (L && !R)playerFR = false; else if (R && !L)playerFR = true;
        int pw = imgPR.getwidth() / 2, ph = imgPR.getheight() / 2;
        if (playerX - pw < ARENA_L)playerX = (double)(ARENA_L + pw);
        if (playerX + pw > ARENA_R)playerX = (double)(ARENA_R - pw);
        if (playerY - ph < ARENA_T)playerY = (double)(ARENA_T + ph);
        if (playerY + ph > ARENA_B)playerY = (double)(ARENA_B - ph);

        // Death check (before regen!)
        static bool plDead = false;
        if (playerHP <= 0.0 && !plDead) plDead = true;
        if (plDead) {
            cleardevice(); setfillcolor(BLACK); solidrectangle(0, 0, WIN_W, WIN_H);
            settextcolor(RED); settextstyle(30, 0, L"Consolas");
            const wchar_t* dm = L"You died from four-degree burns";
            outtextxy(CX - textwidth(dm) / 2, CY - 40, dm);
            settextcolor(WHITE); settextstyle(24, 0, L"Consolas");
            const wchar_t* rst = L"Press R to restart";
            outtextxy(CX - textwidth(rst) / 2, CY + 10, rst);
            FlushBatchDraw();
            if (GetAsyncKeyState('R') & 0x8000) {
                TCHAR path[MAX_PATH]; GetModuleFileName(NULL, path, MAX_PATH);
                ShellExecute(NULL, L"open", path, NULL, NULL, SW_SHOW);
                exit(0);
            }
            int el = (int)((clock() - fs) * 1000 / CLOCKS_PER_SEC);
            int rm = 1000 / 60 - el; if (rm > 0) Sleep(rm);
            continue;
        }

        // Invincibility + HP regen + Potion
        if (invincibleTimer > 0.0) { invincibleTimer -= dt; playerVisible = (fmod(invincibleTimer * 12.5, 1.0) < 0.5); if (invincibleTimer <= 0.0) { invincibleTimer = 0.0; playerVisible = true; } }
        else playerVisible = true;
        if (playerHP < playerMaxHP) { playerHP += PL_HP_REGEN * dt; if (playerHP > playerMaxHP) playerHP = playerMaxHP; }
        if (potionCD > 0.0) potionCD -= dt;
        {
            static bool hDn = false; bool hD = (GetAsyncKeyState('H') & 0x8000) != 0;
            if (hD && !hDn && potionCD <= 0.0) { playerHP += 300.0; if (playerHP > playerMaxHP) playerHP = playerMaxHP; potionCD = 60.0; } hDn = hD;
        }

        // Player fire (hold LMB toward mouse)
        static double fireCD = 0.0; fireCD -= dt;
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) && fireCD <= 0.0) {
            fireCD = 2.0 / 45.0;
            POINT mp; GetCursorPos(&mp); ScreenToClient(GetHWnd(), &mp);
            spawnFire(playerX, playerY, (double)mp.x, (double)mp.y);
        }

        // ===== Boss AI =====
        auto trackAbove = [&]() {
            bossTX = playerX; bossTY = playerY - BOSS_ABOVE_Y;
            double dx = bossTX - bossX, dy = bossTY - bossY, dist = std::sqrt(dx * dx + dy * dy);
            double t = 1.0 - std::exp(-BOSS_RESP * dt);
            if (dist < 1.0) { bossX = bossTX;bossY = bossTY; }
            else { bossX += dx * t;bossY += dy * t; }
            };
        auto trackNormal = [&]() {
            bossPhaseTimer -= dt;
            if (bossPhaseTimer <= 0.0) { bossPhase = (bossPhase + 1) % 4;bossPhaseTimer = BOSS_PHASE_DUR; }
            switch (bossPhase) {
            case 0:case 2:bossTX = playerX;bossTY = playerY - BOSS_ABOVE_Y;break;
            case 1:bossTX = playerX - BOSS_SIDE_X;bossTY = playerY;break;
            case 3:bossTX = playerX + BOSS_SIDE_X;bossTY = playerY;break;
            }
            double dx = bossTX - bossX, dy = bossTY - bossY, dist = std::sqrt(dx * dx + dy * dy);
            double t = 1.0 - std::exp(-BOSS_RESP * dt);
            if (dist < 1.0) { bossX = bossTX;bossY = bossTY; }
            else { bossX += dx * t;bossY += dy * t; }
            };

        // TEST: L key = boss -5% HP
        {
            static bool lDn = false; bool lD = (GetAsyncKeyState('L') & 0x8000) != 0;
            if (lD && !lDn) { bossHP -= 5.0; if (bossHP < 0)bossHP = 0; } lDn = lD;
        }

        // Skull spawn (during BH#5)
        if (skullActive) {
            static double skullCD = 0.0; skullCD -= dt;
            if (skullCD <= 0.0) {
                skullCD = SKULL_CD_MIN + (rand() % 1000) / 1000.0 * (SKULL_CD_MAX - SKULL_CD_MIN);
                bool r = (rand() % 2) == 0;
                double sx = r ? 1700.0 : -200.0;
                double sy = (double)(ARENA_T + rand() % (ARENA_B - ARENA_T));
                for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                    delete proj[i]; proj[i] = new SkullProj(sx, sy, SKULL_SPD, !r, t2p(2), 1.0); break;
                }
            }
        }

        // Worm AI
        int bossW = imgBR.getwidth();
        if (wormLen > 0) {
            wormStT -= dt;
            if (wormSt == WORM_RAND) {
                wormTurnT -= dt; double tA = std::atan2(wormHVy, wormHVx);
                if (wormTurnT <= 0.0) { wormTurnT = WORM_TURN;tA = (rand() % 6283) / 1000.0; }
                int sz = WORM_SEG_SZ / 2;
                if (worm[wormLen - 1].x - sz < ARENA_L && wormHVx < 0)tA = 0;
                if (worm[wormLen - 1].x + sz > ARENA_R && wormHVx > 0)tA = PI;
                if (worm[wormLen - 1].y - sz < ARENA_T && wormHVy < 0)tA = PI / 2.0;
                if (worm[wormLen - 1].y + sz > ARENA_B && wormHVy > 0)tA = -PI / 2.0;
                double cA = std::atan2(wormHVy, wormHVx), diff = tA - cA;
                while (diff > PI)diff -= 2 * PI;while (diff < -PI)diff += 2 * PI;double mT = 3.0 * dt;
                if (diff > mT)diff = mT;else if (diff < -mT)diff = -mT;double nA = cA + diff;
                wormHVx = std::cos(nA) * WORM_SPD;wormHVy = std::sin(nA) * WORM_SPD;
                if (wormStT <= 0.0)wormSt = WORM_TRACK;
            }
            else {
                double dx = bossX - worm[wormLen - 1].x, dy = bossY - worm[wormLen - 1].y;
                double dist = std::sqrt(dx * dx + dy * dy), dA = std::atan2(dy, dx), cA = std::atan2(wormHVy, wormHVx);
                double diff = dA - cA;while (diff > PI)diff -= 2 * PI;while (diff < -PI)diff += 2 * PI;double mT = 3.0 * dt;
                if (diff > mT)diff = mT;else if (diff < -mT)diff = -mT;double nA = cA + diff;
                wormHVx = std::cos(nA) * WORM_SPD;wormHVy = std::sin(nA) * WORM_SPD;
                if (dist < (bossW + WORM_SEG_SZ) / 2.0) {
                    for (int k = 0;k < 26;k++) {
                        double a = k * 2.0 * PI / 26;
                        for (int i = 0;i < MAX_PROJ;i++)if (!proj[i] || !proj[i]->active) { delete proj[i];proj[i] = new SulfurProj(bossX, bossY, a, 37.5 * 390.0 / 78.125);proj[i]->accel = WORM_RING_ACC;break; }
                    }
                    wormSt = WORM_RAND;wormStT = 4.0;
                }
            }
            worm[wormLen - 1].x += wormHVx * dt;worm[wormLen - 1].y += wormHVy * dt;
            for (int i = wormLen - 2;i >= 0;i--) { double dx = worm[i + 1].x - worm[i].x, dy = worm[i + 1].y - worm[i].y, dist = std::sqrt(dx * dx + dy * dy);double target = WORM_SEG_SZ * 0.85;if (dist > target) { worm[i].x += dx / dist * (dist - target);worm[i].y += dy / dist * (dist - target); } }
        }

        // Moon
        if (moonActive) {
            if (moonDmgDelay > 0.0) moonDmgDelay -= dt;
            double dx = playerX - moonX, dy = playerY - moonY, dist = std::sqrt(dx * dx + dy * dy);
            if (dist > 1.0) { moonX += dx / dist * MOON_SPD * dt; moonY += dy / dist * MOON_SPD * dt; }
            if (dist < MOON_R && moonDmgDelay <= 0.0) dmgPlayer(DMG_MOON * dt);
        }

        // Eyes AI
        if (eyesActive) {
            int alive = 0;
            for (int i = 0;i < EYE_COUNT;i++) {
                if (!eyes[i].alive)continue; alive++;
                eyes[i].x = bossX + std::cos(eyes[i].angle) * EYE_DIST;
                eyes[i].y = bossY + std::sin(eyes[i].angle) * EYE_DIST;
            }
            if (alive == 0) eyesActive = false;
            else {
                eyeFireCD -= dt;
                if (eyeFireCD <= 0.0) {
                    eyeFireCD = 4.0;
                    for (int i = 0;i < EYE_COUNT;i++) {
                        if (!eyes[i].alive)continue;
                        double a = std::atan2(playerY - eyes[i].y, playerX - eyes[i].x);
                        for (int j = 0;j < MAX_PROJ;j++) if (!proj[j] || !proj[j]->active) {
                            delete proj[j]; proj[j] = new SulfurProj(eyes[i].x, eyes[i].y, a, 37.5 * 390.0 / 78.125); break;
                        }
                    }
                }
            }
        }

        // Brothers AI
        if (brothersActive) {
            broAttackTimer -= dt;
            if (!broAttacking) {
                auto track = [&](Brother& b) { double tx = playerX + (b.onLeft ? -BRO_DIST : BRO_DIST); b.x += (tx - b.x) * 0.08; b.y += (playerY - b.y) * 0.08; };
                if (broA.alive)track(broA); if (broB.alive)track(broB);
                if (broAttackTimer <= 0.0) { broAttacking = true; broFistCount = 0; broAttackTimer = -1; }
            }
            else if (broAttackTimer < 0 && broFistCount < BRO_FIST_TOTAL) {
                if (broA.alive) {
                    double a = std::atan2(playerY - broA.y, playerX - broA.x);
                    for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) { delete proj[i]; proj[i] = new FistProj(broA.x, broA.y, a, BRO_FIST_SPD, BRO_FIST_ACC, broFistToggle); broFistToggle = 1 - broFistToggle; break; }
                }
                if (broB.alive) {
                    double a = std::atan2(playerY - broB.y, playerX - broB.x); bool fast = (broFistCount % 3 == 2);
                    for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) { delete proj[i]; proj[i] = new BladeProj(broB.x, broB.y, a, fast ? BLADE_FAST_SPEED : BRO_FIST_SPD, fast ? 0 : BRO_FIST_ACC, broBladeToggle); broBladeToggle = 1 - broBladeToggle; break; }
                }
                broFistCount++;
                if (broFistCount >= BRO_FIST_TOTAL)broAttackTimer = 0.6;
                else { double t = (double)broFistCount / (BRO_FIST_TOTAL - 1);broAttackTimer = BRO_FIST_INTV_START + (BRO_FIST_INTV_END - BRO_FIST_INTV_START) * t; }
            }
            if (broAttacking && broFistCount >= BRO_FIST_TOTAL && broAttackTimer <= 0.0) { broAttacking = false; broAttackTimer = 2.0; bool aL = broA.onLeft; broA.onLeft = broB.onLeft; broB.onLeft = aL; }
            // Death check
            if (!broA.alive && !broB.alive) brothersActive = false;
        }

        if (bulletHellActive || brothersActive || f_end) { /* boss frozen */ }
        else {

            switch (bossState) {
            case NORMAL:
                bossTransparent = false; bossAlpha = 1.0;
                trackNormal();
                // Phase2 opening: fire 4 bursts sulfur then start from pattern end
                if (phase2Opening) { phase2Opening = false; patternIndex = PATTERN_LEN - 1; bossState = SKILL2_PREP; skill2Timer = SKILL2_PREP_TIME; skill2CurInterval = SKILL2_BURST_INTERVAL - 0.5; skill2BurstsTotal = 4; skill2BurstsRemaining = 4; break; }
                // Pattern countdown
                patternCooldown -= dt;
                if (patternCooldown <= 0.0) {
                    const PtnStep& s = PATTERN[patternIndex]; int cnt = s.count;
                    switch (s.skill) {
                    case PSK_SULFUR: bossState = SKILL2_PREP; skill2Timer = SKILL2_PREP_TIME; skill2BurstsTotal = (cnt > 0) ? cnt : ((rand() % 2) + 3); skill2BurstsRemaining = skill2BurstsTotal; skill2CurInterval = SKILL2_BURST_INTERVAL; break;
                    case PSK_WRAITH: bossState = SKILL4; skill4BurstsTotal = (cnt > 0) ? cnt : 20; if (bossWeak)skill4BurstsTotal = (skill4BurstsTotal + 1) / 2; skill4BurstsRemaining = skill4BurstsTotal; skill4Timer = 0; break;
                    case PSK_ABYSS: bossState = SKILL3; skill3BurstsTotal = (cnt > 0) ? cnt : ((rand() % 2) + 3); skill3BurstsRemaining = skill3BurstsTotal; skill3Timer = 0; break;
                    case PSK_DASH: bossState = DASH_WINDUP; dashTimer = WINDUP_TIME; dashTotal = (cnt > 0) ? cnt : ((rand() % 2) + 2); if (phase2)dashTotal = (dashTotal + 1) / 2; dashRemain = dashTotal; dashHalf = (patternIndex == 7 || patternIndex == 12 || patternIndex == 17 || patternIndex == 19); bossTransparent = true; bossAlpha = TRANSPARENT_ALPHA; break;
                    }
                }
                break;

            case DASH_WINDUP:
                bossTransparent = true; bossAlpha = TRANSPARENT_ALPHA;
                dashTimer -= dt;
                if (dashTimer <= 0.0) {
                    dashTX = playerX; dashTY = playerY;
                    double dx = dashTX - bossX, dy = dashTY - bossY;
                    dashAngle = std::atan2(dy, dx);
                    dashDist = dashHalf ? t2p(50) : t2p(100);
                    dashTraveled = 0.0;
                    bossState = DASH;
                }
                break;

            case DASH_PAUSE:
                dashTimer -= dt;
                if (dashTimer <= 0.0) {
                    bossState = DASH_WINDUP; dashTimer = WINDUP_TIME;
                    bossTransparent = true; bossAlpha = TRANSPARENT_ALPHA;
                }
                break;

            case SKILL2_PREP:
                trackAbove();
                skill2Timer -= dt;
                if (skill2Timer <= 0.0) {
                    bossState = SKILL2_BURST;
                    skill2Timer = 0.0;  // fire immediately
                }
                break;

            case SKILL2_BURST:
                trackAbove();
                skill2Timer -= dt;
                if (skill2Timer <= 0.0 && skill2BurstsRemaining > 0) {
                    if ((rand() % 100) < 30) { // 30% fission
                        double a = std::atan2(playerY - bossY, playerX - bossX);
                        for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                            delete proj[i]; proj[i] = new FissionProj(bossX, bossY, a, 30.0 * 390.0 / 78.125, 3.0, &playerX, &playerY); break;
                        }
                    }
                    else {
                        double cA = std::atan2(playerY - bossY, playerX - bossX);
                        double hS = SKILL2_FAN / 2.0;
                        for (int k = 0;k < SKILL2_PER_BURST;k++) {
                            double a = cA - hS + k * SKILL2_FAN / (SKILL2_PER_BURST - 1);
                            for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                                delete proj[i]; proj[i] = new SulfurProj(bossX, bossY, a, SKILL2_SPD); break;
                            }
                        }
                    }
                    skill2BurstsRemaining--;
                    skill2Timer = skill2CurInterval;
                }
                if (skill2BurstsRemaining <= 0 && skill2Timer <= 0.0) {
                    bossState = NORMAL;
                    patternIndex = (patternIndex + 1) % PATTERN_LEN;
                    patternCooldown = 1.5;
                }
                break;

            case SKILL3:
                trackAbove();
                skill3Timer -= dt;
                if (skill3Timer <= 0.0 && skill3BurstsRemaining > 0) {
                    double a = std::atan2(playerY - bossY, playerX - bossX);
                    for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                        delete proj[i]; proj[i] = new AbyssProj(bossX, bossY, a, SKILL3_SPD, SKILL3_TURN, &playerX, &playerY); break;
                    }
                    skill3BurstsRemaining--; skill3Timer = ABYSS_BURST_INTERVAL;
                }
                if (skill3BurstsRemaining <= 0 && skill3Timer <= 0.0) {
                    bossState = NORMAL;
                    patternIndex = (patternIndex + 1) % PATTERN_LEN;
                    patternCooldown = 1.5;
                }
                break;

            case SKILL4:
                // Track player at same Y level, stay on one side
            {
                bool fireR = (bossX >= playerX);
                bossTX = playerX + (fireR ? SKILL4_SIDE_X : -SKILL4_SIDE_X);
                bossTY = playerY;
                double dx = bossTX - bossX, dy = bossTY - bossY, dist = std::sqrt(dx * dx + dy * dy);
                double t = 1.0 - std::exp(-BOSS_RESP * 2.0 * dt);
                if (dist < 1.0) { bossX = bossTX;bossY = bossTY; }
                else { bossX += dx * t;bossY += dy * t; }
            }
            skill4Timer -= dt;
            if (skill4Timer <= 0.0 && skill4BurstsRemaining > 0) {
                double dir = (playerX > bossX) ? 1.0 : -1.0;
                for (int i = 0;i < MAX_PROJ;i++) if (!proj[i] || !proj[i]->active) {
                    delete proj[i]; proj[i] = new WraithProj(bossX, bossY, dir, SKILL4_SPD, SKILL4_ACC); break;
                }
                skill4BurstsRemaining--;
                skill4Timer = 0.35;  // WRAITH_BURST_INTERVAL
            }
            if (skill4BurstsRemaining <= 0 && skill4Timer <= 0.0) {
                bossState = NORMAL;
                patternIndex = (patternIndex + 1) % PATTERN_LEN;
                patternCooldown = 1.5;
            }
            break;

            case DASH:
                // ... (dash logic unchanged but update completion)
            {
                double move = DASH_SPD * dt;
                bossX += std::cos(dashAngle) * move;
                bossY += std::sin(dashAngle) * move;
                dashTraveled += move;
                if (dashTraveled >= dashDist) {
                    dashRemain--;
                    if (dashRemain > 0) { bossState = DASH_PAUSE; dashTimer = PAUSE_TIME; }
                    else { bossState = NORMAL; patternIndex = (patternIndex + 1) % PATTERN_LEN; patternCooldown = 1.5; }
                }
            }
            break;
            }
            if (bossState != DASH_WINDUP && bossState != DASH) bossFR = (bossX >= playerX);

        } // end else (!bulletHellActive)

        // ----- 弹幕更新 -----
        updateProjectiles(dt);

        // Projectile -> player collision
        for (int i = 0;i < MAX_PROJ;i++) {
            if (!proj[i] || !proj[i]->active || proj[i]->type == PROJ_FIRE) continue;
            if (!(invincibleTimer > 0.0) && rectCollide(proj[i]->x, proj[i]->y, proj[i]->hbW(), proj[i]->hbH(), playerX, playerY, PL_HB_W, PL_HB_H)) {
                double d = DMG_SULFUR;
                if (proj[i]->type == PROJ_FISSION || proj[i]->type == PROJ_ABYSS) d = DMG_FISSION;
                else if (proj[i]->type == PROJ_WRAITH || proj[i]->type == PROJ_BHELL) d = DMG_WRAITH;
                else if (proj[i]->type == PROJ_SKULL) d = DMG_SKULL;
                else if (proj[i]->type >= PROJ_FIST1 && proj[i]->type <= PROJ_BLADE2) d = DMG_FIST;
                dmgPlayer(d); invincibleTimer = INVINCIBLE_TIME;
            }
        }

        // Fire hit detection
        for (int i = 0;i < MAX_PROJ;i++) {
            if (!proj[i] || !proj[i]->active || proj[i]->type != PROJ_FIRE) continue;
            bool hit = false; double fw = proj[i]->hbW(), fh = proj[i]->hbH();
            // Boss (not during worm/eyes invincible)
            if (!(wormLen > 0 || eyesActive || bulletHellActive || brothersActive || f_end) && rectCollide(proj[i]->x, proj[i]->y, fw, fh, bossX, bossY, (double)imgBR.getwidth() / 3.0, (double)imgBR.getheight() / 3.0))
            {
                bossHP -= 0.1; if (bossHP < 0)bossHP = 0; hit = true;
            }
            // Brothers
            if (brothersActive) {
                if (broA.alive && rectCollide(proj[i]->x, proj[i]->y, fw, fh, broA.x, broA.y, 30.0, 30.0)) { broA.hp -= 0.5; if (broA.hp <= 0)broA.alive = false; hit = true; }
                if (broB.alive && rectCollide(proj[i]->x, proj[i]->y, fw, fh, broB.x, broB.y, 30.0, 30.0)) { broB.hp -= 0.5; if (broB.hp <= 0)broB.alive = false; hit = true; }
            }
            // Hearts
            for (int k = 0;k < HEART_COUNT;k++) if (hearts[k].active && rectCollide(proj[i]->x, proj[i]->y, fw, fh, hearts[k].x, hearts[k].y, (double)HEART_SIZE_W, (double)HEART_SIZE_H))
            {
                hearts[k].hp -= 10.0; if (hearts[k].hp <= 0)hearts[k].active = false; hit = true;
            }
            // Eyes
            if (eyesActive) for (int k = 0;k < EYE_COUNT;k++) if (eyes[k].alive && rectCollide(proj[i]->x, proj[i]->y, fw, fh, eyes[k].x, eyes[k].y, (double)EYE_SIZE, (double)EYE_SIZE))
            {
                eyes[k].hp -= 10; if (eyes[k].hp <= 0)eyes[k].alive = false; hit = true;
            }
            if (hit) { proj[i]->active = false; delete proj[i]; proj[i] = nullptr; }
        }

        // ----- 绘制 -----
        cleardevice();
        setfillcolor(BLACK);
        solidrectangle(0, 0, WIN_W, ARENA_T); solidrectangle(0, ARENA_B, WIN_W, WIN_H);
        solidrectangle(0, ARENA_T, ARENA_L, ARENA_B); solidrectangle(ARENA_R, ARENA_T, WIN_W, ARENA_B);
        setfillcolor(RGB(20, 5, 10)); solidrectangle(ARENA_L, ARENA_T, ARENA_R, ARENA_B);

        // Moon (bottom)
        if (moonActive) putimageT_Rot((int)moonX, (int)moonY, MOON_W, MOON_H, &imgMoon, 0.0);

        // Eyes
        if (eyesActive) {
            int sz = EYE_SIZE;
            for (int i = 0;i < EYE_COUNT;i++) {
                if (!eyes[i].alive)continue;
                double a = std::atan2(playerY - eyes[i].y, playerX - eyes[i].x) + PI / 2.0;
                putimageT_Rot((int)eyes[i].x, (int)eyes[i].y, sz, sz, &imgEye, a);
            }
        }

        // Brothers
        if (brothersActive) {
            Brother* bs[2] = { &broA,&broB };
            for (int b = 0;b < 2;b++) {
                if (!bs[b]->alive)continue;
                bool fr = (bs[b]->x >= playerX); IMAGE& ci = fr ? *bs[b]->imgR : *bs[b]->imgL;
                int bw = ci.getwidth() / 2, bh = ci.getheight() / 2;
                putimageT_Rot((int)bs[b]->x, (int)bs[b]->y, bw, bh, &ci, 0.0);
            }
        }

        // Hearts
        {
            int hw = HEART_SIZE_W, hh = HEART_SIZE_H;
            for (int i = 0;i < HEART_COUNT;i++) if (hearts[i].active)
                putimageT_Rot((int)hearts[i].x, (int)hearts[i].y, hw, hh, &imgHeart, 0.0);
        }

        // Worm
        if (wormLen > 0) {
            int sz = WORM_SEG_SZ;
            for (int i = 0;i < wormLen;i++) {
                double a = (i == wormLen - 1) ? std::atan2(wormHVy, wormHVx) + PI / 2.0
                    : std::atan2(worm[i + 1].y - worm[i].y, worm[i + 1].x - worm[i].x) + PI / 2.0;
                putimageT_Rot((int)worm[i].x, (int)worm[i].y, sz, sz, worm[i].img, a);
            }
        }

        // 灾厄
        IMAGE& cB = (bulletHellActive || brothersActive || eyesActive || f_end) ? imgBWR : (wormLen > 0 ? (bossFR ? imgBWR : imgBWL) : (bossFR ? imgBR : imgBL));
        if (bossTransparent) putimageT_alpha((int)bossX - cB.getwidth() / 2, (int)bossY - cB.getheight() / 2, &cB, bossAlpha);
        else putimageT((int)bossX - cB.getwidth() / 2, (int)bossY - cB.getheight() / 2, &cB);

        // Projectiles (on top)
        drawProjectiles();

        // Player HP bar + text + potion CD
        {
            int bx = WIN_W - 280, by = 15;
            setfillcolor(RGB(40, 40, 40)); solidrectangle(bx, by, bx + 250, by + 22);
            setfillcolor(RGB(0, 200, 0)); solidrectangle(bx, by, bx + (int)(250 * playerHP / playerMaxHP), by + 22);
            setlinecolor(WHITE); setlinestyle(PS_SOLID, 1); rectangle(bx, by, bx + 250, by + 22);
            TCHAR t[64]; _stprintf_s(t, _T("HP: %.0f / %.0f"), playerHP, playerMaxHP);
            settextcolor(WHITE); settextstyle(16, 0, _T("Consolas")); outtextxy(bx + 5, by + 3, t);
            if (potionCD > 0.0) { settextcolor(RGB(150, 150, 150)); _stprintf_s(t, _T("Potion CD: %.0fs"), potionCD); outtextxy(bx, by + 26, t); }
        }

        // Brother HP bars (bottom-right)
        if (brothersActive) {
            int bx = ARENA_R + 60, by = ARENA_B + 15, bw = 170;
            if (broA.alive) {
                setfillcolor(RGB(40, 40, 40)); solidrectangle(bx, by, bx + bw, by + 14);
                setfillcolor(RGB(255, 180, 60)); solidrectangle(bx, by, bx + (int)(bw * broA.hp / 100.0), by + 14);
                setlinecolor(WHITE); setlinestyle(PS_SOLID, 1); rectangle(bx, by, bx + bw, by + 14);
                TCHAR t[64]; settextcolor(RGB(255, 200, 100)); settextstyle(14, 0, _T("Consolas"));
                _stprintf_s(t, _T("Calamity %.0f%%"), broA.hp); outtextxy(bx + 3, by, t);
                by += 20;
            }
            if (broB.alive) {
                setfillcolor(RGB(40, 40, 40)); solidrectangle(bx, by, bx + bw, by + 14);
                setfillcolor(RGB(60, 180, 255)); solidrectangle(bx, by, bx + (int)(bw * broB.hp / 100.0), by + 14);
                setlinecolor(WHITE); setlinestyle(PS_SOLID, 1); rectangle(bx, by, bx + bw, by + 14);
                TCHAR t[64]; settextcolor(RGB(100, 200, 255)); settextstyle(14, 0, _T("Consolas"));
                _stprintf_s(t, _T("Disaster %.0f%%"), broB.hp); outtextxy(bx + 3, by, t);
            }
        }

        // 玩家
        if (playerVisible) {
            IMAGE& cP = playerFR ? imgPR : imgPL;
            putimageT((int)playerX - cP.getwidth() / 2, (int)playerY - cP.getheight() / 2, &cP);
        }

        // Boss HP bar
        {
            int bx = CX - 200, by = ARENA_B + 15;
            setfillcolor(RGB(40, 40, 40)); solidrectangle(bx, by, bx + 400, by + 22);
            setfillcolor(RGB(200, 40, 40)); solidrectangle(bx, by, bx + (int)(400 * bossHP / 100.0), by + 22);
            setlinecolor(WHITE); setlinestyle(PS_SOLID, 1); rectangle(bx, by, bx + 400, by + 22);
            TCHAR t[64]; _stprintf_s(t, _T("Calamitas %.1f%%"), bossHP);
            settextcolor(RGB(255, 100, 100)); settextstyle(18, 0, _T("Consolas")); outtextxy(bx + 5, by + 3, t);
        }

        setlinecolor(RGB(120, 20, 30)); setlinestyle(PS_SOLID, 2);
        rectangle(ARENA_L, ARENA_T, ARENA_R, ARENA_B);

        FlushBatchDraw();
        int el = (int)((clock() - fs) * 1000 / CLOCKS_PER_SEC);
        int rm = 1000 / 60 - el; if (rm > 0)Sleep(rm);
    }
    clearProjectiles();
    EndBatchDraw(); Gdiplus::GdiplusShutdown(gdptok); closegraph(); return 0;
}
