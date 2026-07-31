#include <iostream>
using namespace std;

template <typename T>
class SafeArray {
public:
    SafeArray(int s);
    T& operator[](int index);
    void sort();
    T sum();
    void print();
protected:
    int size;
    T element[5];
};

template <typename T>
SafeArray<T>::SafeArray(int s) {
    if (s > 5) s = 5;
    if (s < 1) s = 1;
    size = s;
    for (int i = 0; i < size; i++) {
        element[i] = T();
    }
}

template <typename T>
T& SafeArray<T>::operator[](int index) {
    if (index < 0 || index >= size) {
        cout << "警告: 下标 " << index << " 越界!" << endl;
        static T dummy;
        return dummy;
    }
    return element[index];
}

template <typename T>
void SafeArray<T>::sort() {
    for (int i = 1; i < size; i++) {
        T key = element[i];
        int j = i - 1;
        while (j >= 0 && element[j] > key) {
            element[j + 1] = element[j];
            j--;
        }
        element[j + 1] = key;
    }
}

template <typename T>
T SafeArray<T>::sum() {
    T total = T();
    for (int i = 0; i < size; i++) {
        total = total + element[i];
    }
    return total;
}

template <typename T>
void SafeArray<T>::print() {
    for (int i = 0; i < size; i++) {
        cout << element[i] << " ";
    }
    cout << endl;
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

    Box operator+(const Box& other) const {
        return Box(length + other.length,
                   width + other.width,
                   height + other.height);
    }

    friend ostream& operator<<(ostream& os, const Box& b) {
        os << "Box(" << b.length << "x" << b.width
           << "x" << b.height << ",V=" << b.volume() << ")";
        return os;
    }
};

int main() {
    SafeArray<int> intArr(5);
    intArr[0] = 42;
    intArr[1] = 15;
    intArr[2] = 73;
    intArr[3] = 8;
    intArr[4] = 99;

    cout << "int数组 排序前: ";
    intArr.print();

    intArr.sort();
    cout << "int数组 排序后: ";
    intArr.print();

    cout << "int数组 求和: " << intArr.sum() << endl;

    cout << "int数组 越界测试: ";
    intArr[10] = 100;

    SafeArray<Box> boxArr(5);
    boxArr[0] = Box(3, 4, 5);
    boxArr[1] = Box(1, 2, 3);
    boxArr[2] = Box(2, 2, 2);
    boxArr[3] = Box(5, 1, 2);
    boxArr[4] = Box(4, 3, 2);

    cout << "\nBox数组 排序前: ";
    boxArr.print();

    boxArr.sort();
    cout << "Box数组 排序后: ";
    boxArr.print();

    Box total = boxArr.sum();
    cout << "Box数组 求和: " << total << endl;

    cout << "Box数组 越界测试: ";
    boxArr[10] = Box(9, 9, 9);

    return 0;
}
