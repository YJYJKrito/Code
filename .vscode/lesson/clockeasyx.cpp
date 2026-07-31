#include <graphics.h>
#include <conio.h>
#include <cmath>
#include <ctime>

#define PI 3.14159265358979323846

int main()
{
    // 创建窗口，大小与背景图一致 (308x308)
    initgraph(308, 308);

    // 加载表盘背景图
    IMAGE bg;
    loadimage(&bg, _T("assets\\biaopan.png"));

    // 表盘中心坐标
    const int cx = 154;
    const int cy = 154;

    // 指针长度：正方向(圆心到尖端) / 反方向(圆心到尾部)
    const int hourForward  = 58;
    const int hourBackward = 12;
    const int minForward   = 85;
    const int minBackward  = 16;
    const int secForward   = 98;
    const int secBackward  = 28;

    BeginBatchDraw();

    while (!_kbhit())
    {
        // 获取当前系统时间
        SYSTEMTIME st;
        GetLocalTime(&st);

        // 计算各指针角度（从12点方向起算，顺时针为正）
        double secAngle  = st.wSecond * PI / 30.0 - PI / 2.0;
        double minAngle  = (st.wMinute + st.wSecond / 60.0) * PI / 30.0 - PI / 2.0;
        double hourAngle = ((st.wHour % 12) + st.wMinute / 60.0) * PI / 6.0 - PI / 2.0;

        // 清屏并绘制背景
        cleardevice();
        putimage(0, 0, &bg);

        // --- 时针（粗、短） ---
        setlinestyle(PS_SOLID, 7);
        setlinecolor(BLACK);
        {
            int x0 = cx - (int)(hourBackward * cos(hourAngle));
            int y0 = cy - (int)(hourBackward * sin(hourAngle));
            int x1 = cx + (int)(hourForward  * cos(hourAngle));
            int y1 = cy + (int)(hourForward  * sin(hourAngle));
            line(x0, y0, x1, y1);
        }

        // --- 分针（中等） ---
        setlinestyle(PS_SOLID, 5);
        setlinecolor(BLACK);
        {
            int x0 = cx - (int)(minBackward * cos(minAngle));
            int y0 = cy - (int)(minBackward * sin(minAngle));
            int x1 = cx + (int)(minForward  * cos(minAngle));
            int y1 = cy + (int)(minForward  * sin(minAngle));
            line(x0, y0, x1, y1);
        }

        // --- 秒针（细、红） ---
        setlinestyle(PS_SOLID, 2);
        setlinecolor(RED);
        {
            int x0 = cx - (int)(secBackward * cos(secAngle));
            int y0 = cy - (int)(secBackward * sin(secAngle));
            int x1 = cx + (int)(secForward  * cos(secAngle));
            int y1 = cy + (int)(secForward  * sin(secAngle));
            line(x0, y0, x1, y1);
        }

        // --- 中心帽 ---
        setfillcolor(BLACK);
        solidcircle(cx, cy, 6);
        setfillcolor(RED);
        solidcircle(cx, cy, 3);

        FlushBatchDraw();
        Sleep(40);
    }

    EndBatchDraw();
    closegraph();
    return 0;
}
