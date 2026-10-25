#include <iostream>
using namespace std;

class LinkList
{
private:
    struct Node
    {
        int data;
        Node *next;
    };
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

    void Print(const char *tag)
    {
        cout << tag;
        Node *p = head->next;
        while (p != NULL)
        {
            cout << p->data << " ";
            p = p->next;
        }
        cout << endl;
    }

    void Invert()
    {
        Node *p = head->next;
        head->next = NULL;
        while (p != NULL)
        {
            Node *q = p->next;
            p->next = head->next;
            head->next = p;
            p = q;
        }
    }
};

int main()
{
    int n;
    LinkList list;

    cin >> n;
    list.Create(n);
    list.Print("倒置前为：");

    list.Invert();
    list.Print("倒置后为：");

    return 0;
}
