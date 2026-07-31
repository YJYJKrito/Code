#include <iostream>
using namespace std;

template <typename T>
T min(T a[], int n) {
    T minVal = a[0];
    for (int i = 1; i < n; i++) {
        if (a[i] < minVal) {
            minVal = a[i];
        }
    }
    return minVal;
}

int main() {
    int intArr[] = {3, 1, 4, 1, 5, 9, 2, 6};
    double doubleArr[] = {3.14, 1.41, 2.72, 0.58, 1.62};

    int intN = sizeof(intArr) / sizeof(intArr[0]);
    int doubleN = sizeof(doubleArr) / sizeof(doubleArr[0]);

    cout << "int 数组的最小值: " << min(intArr, intN) << endl;
    cout << "double 数组的最小值: " << min(doubleArr, doubleN) << endl;

    return 0;
}
