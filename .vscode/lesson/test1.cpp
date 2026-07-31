#include <iostream>
#include <cstdlib>
using namespace std;


void swap_pointer(int* a, int* b) {
    int temp = *a;  
    *a = *b;
    *b = temp;
}


void swap_reference(int& a, int& b) {  
    int temp = a;
    a = b;
    b = temp;
}

int main() {
    system("chcp 65001 > nul");  // 设置控制台为 UTF-8 编码
    int x = 10, y = 20;

    cout << "初始状态: x = " << x << ", y = " << y << endl;

    
    swap_pointer(&x, &y);
    cout << "指针交换后: x = " << x << ", y = " << y << endl;

    
    swap_reference(x, y);
    cout << "引用交换后: x = " << x << ", y = " << y << endl;

    return 0;
}
