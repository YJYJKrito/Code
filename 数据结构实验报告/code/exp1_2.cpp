#include <iostream>
using namespace std;

struct Node
{
    int data;
    Node *next;
};

Node *Create(int n)
{
    Node *head = NULL;
    Node *rear = NULL;
    for (int i = 0; i < n; i++)
    {
        Node *p = new Node;
        cin >> p->data;
        p->next = NULL;
        if (head == NULL)
            head = p;
        else
            rear->next = p;
        rear = p;
    }
    return head;
}

int Max(Node *head)
{
    if (head->next == NULL)
        return head->data;
    int m = Max(head->next);
    return head->data > m ? head->data : m;
}

int Count(Node *head)
{
    if (head == NULL)
        return 0;
    return 1 + Count(head->next);
}

double Average(Node *head, int &n)
{
    if (head == NULL)
    {
        n = 0;
        return 0;
    }
    double avg = Average(head->next, n);
    n++;
    return (avg * (n - 1) + head->data) / n;
}

void Print(Node *head)
{
    Node *p = head;
    while (p != NULL)
    {
        cout << p->data << " ";
        p = p->next;
    }
    cout << endl;
}

int main()
{
    int n;
    cin >> n;
    Node *head = Create(n);

    cout << "链表中的数据为:";
    if (head == NULL)
        cout << "(空表)" << endl;
    else
        Print(head);

    if (head == NULL)
    {
        cout << "链表为空,不能求最大值和平均值" << endl;
        cout << "结点个数为:0" << endl;
    }
    else
    {
        cout << "链表中的最大值为:" << Max(head) << endl;
        cout << "链表中结点的个数为:" << Count(head) << endl;
        int num;
        double avg = Average(head, num);
        cout << "链表中所有整数的平均值为:" << avg << endl;
    }

    return 0;
}
