#include <iostream>
using namespace std;

class Clock {
private:
    int hour, minute, second;
public:
    // 默认构造函数
    Clock() {
        hour = 0;
        minute = 0;
        second = 0;
    }

    // 带参构造函数
    Clock(int h, int m, int s) {
        hour = h;
        minute = m;
        second = s;
    }

    // 析构函数
    ~Clock() {
        cout << "对象销毁: " << hour << ":" << minute << ":" << second << endl;
    }

    void setclock(int h, int m, int s) {
        this->hour = h;
        this->minute = m;
        this->second = s;
    }

    void showclock() {
        cout << "当前时间: " << hour << ":" << minute << ":" << second << endl;
    }

    void tick() {
        second++;
        if (second == 60) {
            second = 0;
            minute++;
            if (minute == 60) {
                minute = 0;
                hour++;
                if (hour == 24) {
                    hour = 0;
                }
            }
        }
        showclock();
    }
};

int main() {
    Clock clock1;                        // 调用默认构造函数
    Clock clock2(11, 12, 13);            // 调用带参构造函数
    Clock clock3(8, 30, 0);              // 调用带参构造函数

    clock1.showclock();                  // 默认初始化为 0:0:0
    clock2.showclock();                  // 11:12:13
    clock3.showclock();                  // 8:30:0

    clock1.setclock(12, 12, 12);         // 用 setclock 修改 clock1
    clock1.showclock();                  // 12:12:12

    // 演示 tick 功能：给 clock2 加 1 秒
    cout << "\n--- tick 演示 ---" << endl;
    clock2.showclock();
    clock2.tick();                       // 11:12:14
    clock2.tick();                       // 11:12:15
    clock2.tick();                       // 11:12:16

    return 0;
}
