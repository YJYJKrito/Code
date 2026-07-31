#include <iostream>

using namespace std;

class Room
{
protected:
    double length;
    double width;
public:
    Room(double l, double w) :length(l),width(w){}
};

class ClassRoom : public Room {
private:
    int seat;
public:
    ClassRoom(int s, double l, double w) : Room(l, w) {
        seat = s;
    }

    double calAverArea() {
        return (length * width) / seat;
    }

    void show() {
        cout << "创建房间：" << length << "米, " << width << "米, "
             << seat << "个座位" << endl;
        cout << "长" << length << "米, 宽" << width << "米, "
             << seat << "个座位, 平均面积" << calAverArea() << "平米" << endl;
    }
};

int main() {
    ClassRoom cr(30, 12, 10);
    cr.show();
    return 0;
}
