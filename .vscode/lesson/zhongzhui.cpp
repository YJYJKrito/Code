/**栈的应用：计算器：中缀表达式转换为后缀，并计算

    完善顺序栈的类定义（这部分和 栈的定义练习04 中代码相同）
    完善Caculator类中如下两个成员函数实现：
	void infix_to_suffix();   //将中缀表达式转换为后缀表达
	void cal_suffix();        //计算中缀表达式的值

**/
#include <cstdlib>
#include <iostream>
#include <string>

using namespace std;

template <class T>
class ArrayStack {
private:
    int maxSize; //栈中最多保存的元素个数
    int top; //指向栈顶元素空间编号，初始化为-1，入栈时top+1，出栈时top-1
    T* st; //栈的元素存储的起始地址
public:
    ArrayStack(int sz=100);
    ~ArrayStack();
    void Clear();
    bool isEmpty();
    bool isFull();

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
    if (top == maxSize - 1)
        {
            cout << "栈满溢出" << endl;
            return false;
        }
    else
        {
            st[++top] = item;
            return true;
        }
}
/*
TODO:b. 出栈操作，并判断栈是否为空，若为空，则打印cout << "栈为空，不能进行删除操作" << endl;返回false。
否则，将栈顶元素的值取出来赋值给item，进行出栈操作，返回true
返回值说明：如果出栈成功，返回true，否则返回false。
*/
template <class T>
bool ArrayStack<T>::Pop(T& item)
{
    if (top == -1) {
        cout << "栈为空，不能进行删除操作" << endl;
        return false;
    }
    else
    {
        item = st[top--];
        return true;
    }
}
/*
TODO:c. 读取栈顶元素，但不删除，判断栈是否为空,若为空，则打印cout << "栈为空，不能读取栈顶元素" << endl;返回false
否则，将栈顶元素取出，并赋值给item，返回true。
返回值说明：成功读取栈顶元素，返回true，否则返回false。
*/
template <class T>
bool ArrayStack<T>::Top(T& item)
{
    if (top == -1) {
        cout << "栈为空，不能读取栈顶元素" << endl;
        return false;
    }
    else
    {
        item = st[top];
        return true;
    }
}





class Calculator {
public:
    string infix; //中缀表达式
    string postfix; //后缀表达式
    ArrayStack<double> poststack; //后缀栈
    Calculator(string str);
    ~Calculator()
    {
        poststack.Clear();
    }
    void infix_to_suffix(); //TODO:e. 将中缀表达式转换为后缀表达
    int inStack(char ch); //返回ch代表的运算符号栈内优先级
    int outStack(char ch); //返回ch代表的运算符号栈外优先级数
    void cal_suffix(); //TODO:d.计算中缀表达式的值
    void print();
    bool Get_operands(double& left, double& right);
    bool isNumber(char ch);//判断ch代表的符号是否是数字
    double returnnum(char* c, int n); //返回字符对应的数
    float toNum(char num_ch); //换成数字
    void cal(char ch); //根据操作符 去栈中去取两个元素计算
};
Calculator::Calculator(string str)
{
    infix = str;
}

/*
TODO:e.将中缀表达式转换为后缀表达,假如中缀表达式为1+(2-3)*4+4/2，
则转换后为：1&2&3&-4&*+4&2&/+
为了区分数字与数字，用&符号将数字进行隔开，当然也可自行选择其他方式来表达]
 */
void Calculator::infix_to_suffix()
{
    ArrayStack<char> opstack;
    opstack.Push('#');
    postfix = "";
    int i = 0;
    int len = infix.length();
    while (i < len) {
        char ch = infix[i];
        if (isNumber(ch)) {
            while (i < len && isNumber(infix[i])) {
                postfix += infix[i];
                i++;
            }
            postfix += '&';
        }
        else if (ch == '+' || ch == '-' || ch == '*' || ch == '/' || ch == '%' || ch == '(' || ch == ')') {
            char topch;
            opstack.Top(topch);
            if (ch == ')' && topch == '#') {
                cout << "重新输入 并检查表达式的合法性" << endl;
                exit(1);
            }
            if (inStack(topch) < outStack(ch)) {
                opstack.Push(ch);
                i++;
            }
            else if (inStack(topch) > outStack(ch)) {
                opstack.Pop(topch);
                postfix += topch;
            }
            else {
                opstack.Pop(topch);
                i++;
            }
        }
        else {
            cout << "重新输入 并检查表达式的合法性" << endl;
            exit(1);
        }
    }
    char topch;
    while (opstack.Top(topch) && topch != '#') {
        opstack.Pop(topch);
        if (topch == '(') {
            cout << "重新输入 并检查表达式的合法性" << endl;
            exit(1);
        }
        postfix += topch;
    }
}

/*
TODO: d.计算中缀表达式的值，比如：假如中缀表达式为1+(2-3)*4+4/2，则根据四则混合运算优先级计算，它的结果为-1.
如果输入的中缀表达式非法，则打印cout << "重新输入 并检查表达式的合法性" << endl;，然后exit(1)结束执行。
*/
void Calculator::cal_suffix()
{
    infix_to_suffix(); //将中缀表达式转换为后缀表达式
    int i = 0;
    int len = postfix.length();
    while (i < len) {
        if (isNumber(postfix[i])) {
            char numstr[50];
            int n = 0;
            while (i < len && isNumber(postfix[i])) {
                numstr[n] = postfix[i];
                n++;
                i++;
            }
            poststack.Push(returnnum(numstr, n));
        }
        else if (postfix[i] == '&') {
            i++;
        }
        else {
            cal(postfix[i]);
            i++;
        }
    }
    double value;
    if (!poststack.Pop(value) || !poststack.isEmpty()) {
        cout << "重新输入 并检查表达式的合法性" << endl;
        exit(1);
    }
    poststack.Push(value);
}

bool Calculator::Get_operands(double& left, double& right)
{
    if (poststack.isEmpty()) {
        cout << "缺少右操作数" << endl;
        return false;
    }
    poststack.Pop(right);//取右操作数
    if (poststack.isEmpty()) {
        cout << "缺少左操作数" << endl;
        return false;
    }
    poststack.Pop(left);//取出左操作数
    return true; //返回true
}
double Calculator::returnnum(char* c, int n) //返回字符对应的数,n表示个十百千位
{
    int l = n;
    double num = 0;
    double m = 1;
    for (int i = l - 1; i >= 0; i--) {
        num += toNum(c[i]) * m;
        m *= 10;
    }
    return num;
}

int Calculator::inStack(char c)
{
    switch (c) {
    case '#':
        return 0;
        break;
    case '(':
        return 1;
        break;
    case '*':
    case '/':
    case '%':
        return 5;
        break;
    case '+':
    case '-':
        return 3;
        break;
    case ')':
        return 6;
        break;
    }
    return -1;//超星平台上，如果这条return 会编译不通过
}
//返回操作符的栈外优先级
int Calculator::outStack(char c)
{
    switch (c) {
    case '#':
        return 0;
        break;
    case '(':
        return 6;
        break;
    case '*':
    case '/':
    case '%':
        return 4;
        break;
    case '+':
    case '-':
        return 2;
        break;
    case ')':
        return 1;
        break;
    }
    return -1;//超星平台上，如果这条return 会编译不通过
}
//判断是否为操作数
bool Calculator::isNumber(char ch)
{
    if (48 <= ch && ch <= 57)
        return true;
    else
        return false;
}

//数值型字符转换为数字
float Calculator::toNum(char num_ch)
{
    switch (num_ch) {
    case '0':
        return 0;
        break;
    case '1':
        return 1;
        break;
    case '2':
        return 2;
        break;
    case '3':
        return 3;
        break;
    case '4':
        return 4;
        break;
    case '5':
        return 5;
        break;
    case '6':
        return 6;
        break;
    case '7':
        return 7;
        break;
    case '8':
        return 8;
        break;
    case '9':
        return 9;
        break;
    }
    return 0;//超星平台上，如果这条return 会编译不通过
}
//根据操作符 去栈中去取两个元素计算
void Calculator::cal(char cp)
{
    double left, right, value;
    if (Get_operands(left, right)) {
        switch (cp) {
        case '+': {
            value = left + right;
            poststack.Push(value);//相加后结果压栈
            break;
        }
        case '-': {
            value = left - right;
             poststack.Push(value);//相减 压栈
            break;
        }
        case '*': {
            value = left * right;
            poststack.Push(value);//相乘 压栈
            break;
        }
        case '/':
            if (right == 0) {
                cout << "/ 操作符 右操作数为0" << endl;
                exit(1);
            } else {
                value = left / right;
               poststack.Push(value);//相除 压栈
            }
            break;
        }
    }
}
//保留并输出最后结果
void Calculator::print()
{
   double result;
   poststack.Pop(result);
   cout<<"the postfix express is:"<<endl;
   cout <<postfix<<endl;
   cout<<"the result is: "<<endl;
   cout <<result<<endl;
}
int main()
{
    string str;
    // << "input an infix expression:" << endl;
    cin >> str;
    Calculator calculator(str);
    calculator.cal_suffix();
    calculator.print(); //显示结果
    return 0;
}
