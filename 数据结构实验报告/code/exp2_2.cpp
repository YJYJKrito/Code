#include <iostream>
using namespace std;

template <class T>
class CirQueue
{
private:
    int maxSize;
    int front;
    int rear;
    T *data;

public:
    CirQueue(int sz = 100)
    {
        maxSize = sz;
        front = rear = 0;
        data = new T[maxSize];
    }

    ~CirQueue()
    {
        delete[] data;
    }

    bool isEmpty()
    {
        return front == rear;
    }

    bool isFull()
    {
        return (rear + 1) % maxSize == front;
    }

    bool EnQueue(const T &x)
    {
        if (isFull())
            return false;
        data[rear] = x;
        rear = (rear + 1) % maxSize;
        return true;
    }

    bool DeQueue(T &x)
    {
        if (isEmpty())
            return false;
        x = data[front];
        front = (front + 1) % maxSize;
        return true;
    }

    bool GetFront(T &x)
    {
        if (isEmpty())
            return false;
        x = data[front];
        return true;
    }

    void Clear()
    {
        front = rear = 0;
    }
};

int main()
{
    int k;
    cin >> k;
    if (k < 2)
    {
        cout << "k必须大于等于2" << endl;
        return 0;
    }

    CirQueue<long long> q(k + 2);
    q.EnQueue(1);
    q.EnQueue(1);

    for (int level = 2; level <= k; level++)
    {
        for (int i = 1; i <= level - 1; i++)
        {
            long long a, b;
            q.DeQueue(a);
            q.GetFront(b);
            q.EnQueue(a + b);
        }
        q.EnQueue(1);
    }

    cout << "(a+b)^" << k << " 展开后各项系数为:";
    while (!q.isEmpty())
    {
        long long x;
        q.DeQueue(x);
        cout << x << " ";
    }
    cout << endl;
    return 0;
}
