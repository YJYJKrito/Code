#include <iostream>
using namespace std;

template <class T>
void mysort(T a[], int n) {
    for (int i = 0; i < n - 1; i++) {
        for (int j = 0; j < n - 1 - i; j++) {
            if (a[j] > a[j + 1]) {
                T temp = a[j];
                a[j] = a[j + 1];
                a[j + 1] = temp;
            }
        }
    }
}

class Box {
private:
    double length;
    double width;
    double height;

public:
    Box(double l = 0, double w = 0, double h = 0)
        : length(l), width(w), height(h) {}

    double volume() const {
        return length * width * height;
    }

    bool operator>(const Box& other) const {
        return this->volume() > other.volume();
    }

    friend ostream& operator<<(ostream& os, const Box& b) {
        os << "Box(" << b.length << " x " << b.width << " x " << b.height
           << ")  体积 = " << b.volume();
        return os;
    }
};

int main() {
    int intArr[] = { 64, 34, 25, 12, 22, 11, 90 };
    int n1 = sizeof(intArr) / sizeof(intArr[0]);

    cout << "===== int 数组测试 =====" << endl;
    cout << "排序前: ";
    for (int i = 0; i < n1; i++) {
        cout << intArr[i] << " ";
    }
    cout << endl;

    mysort(intArr, n1);

    cout << "排序后: ";
    for (int i = 0; i < n1; i++) {
        cout << intArr[i] << " ";
    }
    cout << endl;

    Box boxArr[] = {
        Box(3, 4, 5),
        Box(1, 2, 3),
        Box(2, 2, 2),
        Box(5, 1, 2),
        Box(4, 3, 2)
    };
    int n2 = sizeof(boxArr) / sizeof(boxArr[0]);

    cout << "\n===== Box 数组测试 =====" << endl;
    cout << "排序前:" << endl;
    for (int i = 0; i < n2; i++) {
        cout << "  " << boxArr[i] << endl;
    }

    mysort(boxArr, n2);

    cout << "\n排序后 (按体积升序):" << endl;
    for (int i = 0; i < n2; i++) {
        cout << "  " << boxArr[i] << endl;
    }

    return 0;
}
