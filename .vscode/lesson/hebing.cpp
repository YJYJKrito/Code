/*课后习题
    链表操作03：合并两个带有头结点的有序单链表

	在链表操作练习02 的代码基础上，增加如下的成员函数：
	void Merge(MList& another);
	默认当前链表和another链表中存储的数据都是从小到到大的，
	并且将 another链表的中结点合并到当前链表中，不要开辟新的结点空间。

*/
#include <iostream>
using namespace std;
const int MinNumber = -1000000;

class Node {
public:
    Node(int x)
    {
        data = x;
        next = NULL;
    }
    int data;
    Node* next;
};
class MList //带有头结点的单向链表类定义
{
private:
    Node* head;//指向头结点，不是实际的数据结点

public:
    MList();
    ~MList();
    void insertToTail(int val);//TODO1:和练习2相同，在类外给出该函数实现——向尾部插入数据val向尾部插入数据val
    void Merge(MList& another); //TODO2：在类外给出该函数实现——将another中的数据结点合并到当前链表中
    void print();// 逐一输出链表中的数据
};
//不需要改变下面的构造函数
MList::MList()
{
    head = new Node(0);//head 指向的是头结点
}
//不需要改变下面的析构函数
MList::~MList()
{
    Node* temp = head;
    while(temp) //逐个释放结点空间
    {
        head = head ->next;
        delete temp;
        temp = head;
    }
    head = NULL;
}
//不要改变下面的print函数
void MList::print()
{
    Node* p = head->next;
    while (p) {
        cout << p->data<< " ";
        p = p ->next;
    }
}
void MList::insertToTail(int val)
{
    Node* p = head;
    while (p->next != NULL)
        p = p->next;
    p->next = new Node(val);
}

void MList::Merge(MList& another)
{
    Node* pa = head;
    Node* pb = another.head->next;
    while (pa->next != NULL && pb != NULL)
    {
        if (pa->next->data <= pb->data)
            pa = pa->next;
        else
        {
            Node* q = pb->next;
            pb->next = pa->next;
            pa->next = pb;
            pa = pb;
            pb = q;
        }
    }
    if (pb != NULL)
        pa->next = pb;
    another.head->next = NULL;
}
//不要改变下面的main函数
int main()
{
    MList lt1;//创建链表对象 lt1
    MList lt2; //创建链表对象lt2
    int val;
    int Num;//Num 表示要输入的元素的个数
   //输入第一个链表的数据个数，并从小到大输入各个数据
    cin >> Num;
    for (int i = 0; i < Num; i++) {
        cin >> val;
        lt1.insertToTail(val);
    }
    //输入第二个链表的数据个数，并从小到大输入各个数据
    cin >> Num;
    for (int i = 0; i < Num; i++) {
        cin >> val;
        lt2.insertToTail(val);
    }
    cout << "合并前链表1为:";
    lt1.print();
    cout << endl;
    cout << "合并前链表2为:";
    lt2.print();
    cout << endl;
    lt1.Merge(lt2);
    cout << "合并后的单链表为:";
    lt1.print();
    cout << endl;
    return 0;
}
