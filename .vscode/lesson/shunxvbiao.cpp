#include <iostream>
using namespace std;

class SeqList
{
private:
    int data[100];
    int length;
    
public:
    SeqList() { length = 0; }

    void Create(int n)
    {
        length = n;
        for (int i = 0; i < n; i++)
            cin >> data[i];
    }

    void Print(const char *tag)
    {
        cout << tag;
        for (int i = 0; i < length; i++)
            cout << data[i] << " ";
        cout << endl;
    }

    int DeleteMin()
    {
        int minIndex = 0;
        for (int i = 1; i < length; i++)
            if (data[i] < data[minIndex])
                minIndex = i;
        int minValue = data[minIndex];
        data[minIndex] = data[length - 1];
        length--;
        return minValue;
    }

    void DeleteX(int x)
    {
        int k = 0;
        for (int i = 0; i < length; i++)
            if (data[i] != x)
                data[k++] = data[i];
        length = k;
    }

    void DeleteRange(int s, int t)
    {
        int k = 0;
        for (int i = 0; i < length; i++)
            if (data[i] <= s || data[i] >= t)
                data[k++] = data[i];
        length = k;
    }
};

int main()
{
    int n, x, s, t;
    SeqList list;

    cin >> n;
    list.Create(n);
    list.Print("顺序表数据为:");

    int minValue = list.DeleteMin();
    list.Print("删除最小值后为:");
    cout << "最小值:" << minValue << endl;

    cin >> x;
    list.DeleteX(x);
    list.Print("删除相同值后为:");

    cin >> s >> t;
    list.DeleteRange(s, t);
    list.Print("删除指定范围数值后为:");

    return 0;
}
