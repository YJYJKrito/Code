/* 顺序栈

   完善下面的栈的类定义及成员函数实现
	bool Push(const T item)		//a. 入栈操作，并判断栈是否为满
	bool Pop(T& item)			//b 出栈操作，并判断栈是否为空，返回栈顶元素
	bool Top(T& item)			//c 读取栈顶元素，但不删除，判断栈是否为空

*/
#include <iostream>
using namespace std;


template <class T>
class ArrayStack {
private:
    int maxSize; //栈中最多保存的元素个数
    int top; //指向栈顶元素空间编号，初始化为-1，入栈时top+1，出栈时top-1
    T* st; //栈的元素存储的起始地址
public:
    ArrayStack(int sz);
    ~ArrayStack();
    void Clear();//清空栈中元素
    bool isEmpty();//如果栈空，返回true，否则返回false
    bool isFull();//如果栈满返回true，否则返回false
    /*TODO:a. 入栈操作，并判断栈是否为满,若栈已满，则打印cout << "栈满溢出" << endl;返回false。否则将item入栈，返回true
	返回值说明：成功入栈，返回true，否则返回false。*/
    bool Push(const T& item);
    /*TODO:b. 出栈操作，并判断栈是否为空，若为空，则打印cout << "栈为空，不能进行删除操作" << endl;返回false。
	否则，将栈顶元素的值取出来赋值给item，进行出栈操作，返回true
	返回值说明：如果出栈成功，返回true，否则返回false。*/
    bool Pop(T& item);
    /*TODO:c. 读取栈顶元素，但不删除，判断栈是否为空,若为空，则打印cout << "栈为空，不能读取栈顶元素" << endl;返回false
	否则，将栈顶元素取出，并赋值给item，返回true。
	返回值说明：成功读取栈顶元素，返回true，否则返回false。*/
    bool Top(T& item);
};
template <class T>
ArrayStack<T>::ArrayStack(int sz)
{
    maxSize = sz;
    top = -1;
    st = new T[maxSize];
}
template <class T>
ArrayStack<T>::~ArrayStack()
{
    delete[] st;maxSize = 0; top = -1;
}
template <class T>
void ArrayStack<T>:: Clear()
{
    top = -1;
}

template <class T>
bool ArrayStack<T>::isEmpty()
{
    return top == -1;
}
template <class T>
bool ArrayStack<T>::isFull()
{
    return top == maxSize-1;
}
/*TODO:a. 入栈操作，并判断栈是否为满,若栈已满，则打印cout << "栈满溢出" << endl;返回false。否则将item入栈，返回true
返回值说明：成功入栈，返回true，否则返回false。
*/
template <class T>
bool ArrayStack<T>:: Push(const T& item)
{
    if (isFull())
    {
        cout << "栈满溢出" << endl;
        return false;
    }
    top++;
    st[top] = item;
    return true;
}
/*
TODO:b. 出栈操作，并判断栈是否为空，若为空，则打印cout << "栈为空，不能进行删除操作" << endl;返回false。
否则，将栈顶元素的值取出来赋值给item，进行出栈操作，返回true
返回值说明：如果出栈成功，返回true，否则返回false。
*/
template <class T>
bool ArrayStack<T>::Pop(T& item)
{
    if (isEmpty())
    {
        cout << "栈为空，不能进行删除操作" << endl;
        return false;
    }
    item = st[top];
    top--;
    return true;
}
/*
TODO:c. 读取栈顶元素，但不删除，判断栈是否为空,若为空，则打印cout << "栈为空，不能读取栈顶元素" << endl;返回false
否则，将栈顶元素取出，并赋值给item，返回true。
返回值说明：成功读取栈顶元素，返回true，否则返回false。
*/
template <class T>
bool ArrayStack<T>::Top(T& item)
{
    if (isEmpty())
    {
        cout << "栈为空，不能读取栈顶元素" << endl;
        return false;
    }
    item = st[top];
    return true;
}




//下面的main函数要修改
int main()
{
    int maxsize, loop_num, temp;
  //  cout<<"输入栈的空间大小、从1开始循环压入的整数以及最后要压入栈中的一个整数"<<endl;
    cin >> maxsize>>loop_num>>temp;

    ArrayStack<int> as(maxsize);
    //先循环压入1，2，...,loop_num
    for (int i = 1; i <= loop_num; i++) {
        as.Push(i);
    }

   if(as.Push(temp))
   {
        cout << "入栈：" << temp << endl;
   }

    if(as.Pop(temp))
    {
        cout << "出栈:" << temp << endl;
    }
    if(as.Top(temp))
    {
       cout << "读取栈顶元素:" << temp << endl;
    }
    return 0;
}
