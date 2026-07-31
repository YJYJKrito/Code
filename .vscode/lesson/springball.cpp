#include <graphics.h>
#include <conio.h>
#include <windows.h>

int main()
{
    const int W = 600, H = 800;
    initgraph(W, H);

    // -------- 小球 --------
    int bx = W / 2, by = 80, br = 12;
    int vx = 5, vy = 4;

    // -------- 挡板 --------
    const int pw = 120, ph = 16;
    const int py = H - 50;          // 挡板 Y 坐标（固定）
    int px = W / 2 - pw / 2;        // 挡板 X 坐标
    const int ps = 8;               // 挡板移动速度

    // -------- 状态 --------
    bool paused  = false;
    bool over    = false;
    bool pPrev   = false;           // P 键防抖

    BeginBatchDraw();

    while (!over)
    {
        // ========== 输入处理 ==========

        // P 键 — 暂停/继续（按下瞬间触发）
        bool pNow = (GetAsyncKeyState('P') & 0x8000) != 0;
        if (pNow && !pPrev)
            paused = !paused;
        pPrev = pNow;

        // ESC — 直接退出
        if (GetAsyncKeyState(VK_ESCAPE) & 0x8000)
            break;

        // ========== 游戏逻辑 ==========
        if (!paused)
        {
            // 挡板左右移动（← → 方向键）
            if (GetAsyncKeyState(VK_LEFT) & 0x8000)
                px -= ps;
            if (GetAsyncKeyState(VK_RIGHT) & 0x8000)
                px += ps;

            // 挡板边界约束
            if (px < 0)       px = 0;
            if (px + pw > W)  px = W - pw;

            // 小球移动
            bx += vx;
            by += vy;

            // 左/右墙壁反弹
            if (bx - br <= 0)   { bx = br;      vx = -vx; }
            if (bx + br >= W)   { bx = W - br;  vx = -vx; }

            // 顶部反弹
            if (by - br <= 0)   { by = br;      vy = -vy; }

            // 底部 — 游戏结束
            if (by + br >= H)
            {
                over = true;
            }

            // 挡板碰撞（球向下 + 横向重叠 + 纵向上沿与挡板接触）
            if (!over &&
                vy > 0 &&
                bx + br > px && bx - br < px + pw &&
                by + br >= py && by + br <= py + ph + vy)
            {
                by = py - br;       // 把球弹回挡板上方
                vy = -vy;
            }
        }

        // ========== 绘制 ==========
        cleardevice();

        // 挡板
        setfillcolor(WHITE);
        solidrectangle(px, py, px + pw, py + ph);

        // 小球
        setfillcolor(RED);
        solidcircle(bx, by, br);

        // 暂停提示
        if (paused)
        {
            settextcolor(YELLOW);
            settextstyle(28, 0, _T("Consolas"));
            outtextxy(W / 2 - 50, H / 2 - 20, _T("PAUSED"));
        }

        // 游戏结束提示
        if (over)
        {
            settextcolor(LIGHTRED);
            settextstyle(32, 0, _T("Consolas"));
            outtextxy(W / 2 - 90, H / 2 - 40, _T("GAME OVER"));
            settextstyle(18, 0, _T("Consolas"));
            outtextxy(W / 2 - 110, H / 2 + 10, _T("Press any key to exit..."));
        }

        FlushBatchDraw();
        Sleep(16);   // ≈ 60 fps
    }

    EndBatchDraw();

    // 游戏结束后等待按键
    if (over)
        _getch();

    closegraph();
    return 0;
}
