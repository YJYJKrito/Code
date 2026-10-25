#include <iostream>
using namespace std;

struct Node
{
    int data;
    Node *next;
};

class LinkList
{
private:
    Node *head;

public:
    LinkList()
    {
        head = new Node;
        head->next = NULL;
    }

    void Create(int n)
    {
        Node *rear = head;
        for (int i = 0; i < n; i++)
        {
            Node *p = new Node;
            cin >> p->data;
            p->next = NULL;
            rear->next = p;
            rear = p;
        }
    }

    Node *FindKthFromEnd(int k)
    {
        if (k <= 0)
            return NULL;
        Node *p = head->next;
        Node *q = head->next;
        int i = 0;
        while (i < k && p != NULL)
        {
            p = p->next;
            i++;
        }
        if (i < k)
            return NULL;
        while (p != NULL)
        {
            p = p->next;
            q = q->next;
        }
        return q;
    }

    void Print()
    {
        Node *p = head->next;
        while (p != NULL)
        {
            cout << p->data << " ";
            p = p->next;
        }
        cout << endl;
    }
};

int main()
{
    int n, k;
    LinkList list;

    cin >> n;
    list.Create(n);
    cout << "链表中的数据为:";
    list.Print();

    cin >> k;
    Node *p = list.FindKthFromEnd(k);
    if (p == NULL)
        cout << "倒数第 " << k << " 个结点不存在" << endl;
    else
        cout << "倒数第 " << k << " 个结点的值为:" << p->data << endl;

    return 0;
}
