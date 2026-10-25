#include <iostream>
using namespace std;

int Length(const char *s)
{
    int n = 0;
    while (s[n] != '\0')
        n++;
    return n;
}

int LastPos(const char *str, const char *substr)
{
    int n = Length(str);
    int m = Length(substr);
    if (m == 0 || m > n)
        return 0;
    for (int i = n - m; i >= 0; i--)
    {
        int j = 0;
        while (j < m && str[i + j] == substr[j])
            j++;
        if (j == m)
            return i + 1;
    }
    return 0;
}

int main()
{
    char str[500], substr[500];
    cin >> str >> substr;
    int pos = LastPos(str, substr);
    if (pos == 0)
        cout << "substr 在 str 中不存在" << endl;
    else
        cout << "substr 最后一次出现的位置为:" << pos << endl;
    return 0;
}
