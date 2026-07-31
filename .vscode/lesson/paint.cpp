#include <graphics.h>
#include <conio.h>

int main()
{
    initgraph(800, 600);

    // 默认白色画布 + 白色画笔
    setbkcolor(BLACK);
    cleardevice();

    COLORREF color = WHITE;

    while (true)
    {
        // --- 键盘输入 ---
        if (_kbhit())
        {
            char ch = _getch();
            if (ch == 27)           // ESC — 退出
                break;
            else if (ch == 'c' || ch == 'C')
                cleardevice();      // 清屏
            else if (ch == 'r' || ch == 'R')
                color = RED;
            else if (ch == 'g' || ch == 'G')
                color = GREEN;
            else if (ch == 'b' || ch == 'B')
                color = BLUE;
            else if (ch == 'w' || ch == 'W')
                color = WHITE;
        }

        // --- 鼠标输入 ---
        if (MouseHit())
        {
            MOUSEMSG msg = GetMouseMsg();
            bool ctrl = msg.mkCtrl;

            if (msg.uMsg == WM_LBUTTONDOWN)
            {
                int size = ctrl ? 20 : 10;
                setfillcolor(color);
                solidrectangle(msg.x - size / 2, msg.y - size / 2,
                               msg.x + size / 2, msg.y + size / 2);
            }
            else if (msg.uMsg == WM_RBUTTONDOWN)
            {
                int r = ctrl ? 20 : 10;
                setfillcolor(color);
                solidcircle(msg.x, msg.y, r);
            }
        }
    }

    closegraph();
    return 0;
}
