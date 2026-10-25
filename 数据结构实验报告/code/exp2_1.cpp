#include <iostream>
using namespace std;

template <class T>
class SeqStack
{
private:
    int maxSize;
    int top;
    T *data;

public:
    SeqStack(int sz = 200)
    {
        maxSize = sz;
        top = -1;
        data = new T[maxSize];
    }

    ~SeqStack()
    {
        delete[] data;
    }

    bool isEmpty()
    {
        return top == -1;
    }

    bool isFull()
    {
        return top == maxSize - 1;
    }

    bool Push(const T &x)
    {
        if (isFull())
            return false;
        data[++top] = x;
        return true;
    }

    bool Pop(T &x)
    {
        if (isEmpty())
            return false;
        x = data[top--];
        return true;
    }

    bool Top(T &x)
    {
        if (isEmpty())
            return false;
        x = data[top];
        return true;
    }
};

int Priority(char op)
{
    if (op == '+' || op == '-')
        return 1;
    if (op == '*' || op == '/')
        return 2;
    return 0;
}

char NextChar(const char *s, int &i)
{
    unsigned char a = (unsigned char)s[i];
    unsigned char b = a ? (unsigned char)s[i + 1] : 0;
    if ((a == 0xC3 && b == 0x97) || (a == 0xA1 && b == 0xC1))
    {
        i += 2;
        return '*';
    }
    if ((a == 0xC3 && b == 0xB7) || (a == 0xA1 && b == 0xC2))
    {
        i += 2;
        return '/';
    }
    i++;
    return (char)a;
}

int Calc(SeqStack<double> &num, char op)
{
    double a, b;
    if (!num.Pop(b) || !num.Pop(a))
        return 0;
    if (op == '/' && b == 0)
        return 2;
    double r = 0;
    if (op == '+')
        r = a + b;
    else if (op == '-')
        r = a - b;
    else if (op == '*')
        r = a * b;
    else if (op == '/')
        r = a / b;
    num.Push(r);
    return 1;
}

bool Evaluate(const char *expr, double &result, int &err)
{
    SeqStack<double> num;
    SeqStack<char> op;
    int i = 0;
    while (expr[i] != '\0')
    {
        if (expr[i] == ' ' || expr[i] == '\r')
        {
            i++;
            continue;
        }
        char c = NextChar(expr, i);
        if (c >= '0' && c <= '9')
        {
            num.Push(c - '0');
            continue;
        }
        if (c == '(')
        {
            op.Push(c);
            continue;
        }
        if (c == ')')
        {
            char o;
            while (op.Top(o) && o != '(')
            {
                op.Pop(o);
                int r = Calc(num, o);
                if (r != 1)
                {
                    err = (r == 2) ? 2 : 1;
                    return false;
                }
            }
            if (!op.Pop(o) || o != '(')
            {
                err = 1;
                return false;
            }
            continue;
        }
        if (Priority(c) == 0)
        {
            err = 1;
            return false;
        }
        char o;
        while (op.Top(o) && o != '(' && Priority(o) >= Priority(c))
        {
            op.Pop(o);
            int r = Calc(num, o);
            if (r != 1)
            {
                err = (r == 2) ? 2 : 1;
                return false;
            }
        }
        op.Push(c);
    }
    char o;
    while (op.Pop(o))
    {
        if (o == '(')
        {
            err = 1;
            return false;
        }
        int r = Calc(num, o);
        if (r != 1)
        {
            err = (r == 2) ? 2 : 1;
            return false;
        }
    }
    double x;
    if (!num.Pop(result))
    {
        err = 1;
        return false;
    }
    if (num.Pop(x))
    {
        err = 1;
        return false;
    }
    return true;
}

int main()
{
    char expr[200];
    cin.getline(expr, 200);
    double result;
    int err = 0;
    if (Evaluate(expr, result, err))
        cout << "计算结果为:" << result << endl;
    else if (err == 2)
        cout << "除数不能为0" << endl;
    else
        cout << "表达式错误" << endl;
    return 0;
}
