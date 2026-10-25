#include <iostream>
using namespace std;

struct Node
{
    int data;
    Node *next;
};

Node *Create(int n)
{
    Node *head = new Node;
    head->next = NULL;
    Node *rear = head;
    for (int i = 0; i < n; i++)
    {
        Node *p = new Node;
        cin >> p->data;
        p->next = NULL;
        rear->next = p;
        rear = p;
    }
    return head;
}

void Merge(Node *A, Node *B, Node *&C)
{
    Node *pa = A->next;
    Node *pb = B->next;
    C = A;
    C->next = NULL;
    while (pa != NULL && pb != NULL)
    {
        Node *p;
        if (pa->data <= pb->data)
        {
            p = pa;
            pa = pa->next;
        }
        else
        {
            p = pb;
            pb = pb->next;
        }
        p->next = C->next;
        C->next = p;
    }
    Node *rest = (pa != NULL) ? pa : pb;
    while (rest != NULL)
    {
        Node *p = rest;
        rest = rest->next;
        p->next = C->next;
        C->next = p;
    }
    B->next = NULL;
}

void Print(Node *head)
{
    Node *p = head->next;
    while (p != NULL)
    {
        cout << p->data << " ";
        p = p->next;
    }
    cout << endl;
}

int main()
{
    int m, n;
    Node *A, *B, *C = NULL;

    cin >> m;
    A = Create(m);
    cin >> n;
    B = Create(n);

    cout << "递增有序链表A为:";
    Print(A);
    cout << "递增有序链表B为:";
    Print(B);

    Merge(A, B, C);
    cout << "归并后递减有序链表C为:";
    Print(C);

    return 0;
}
