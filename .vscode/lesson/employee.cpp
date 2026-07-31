#include <iostream>
#include <string>
using namespace std;

const double HOURLY_RATE = 30;   // 计时工资：30元/小时

// ==================== 抽象基类：雇员 ====================
class Employee {
protected:
    string name;                 // 姓名
    int hours;                   // 本月计时工作时长
public:
    Employee(string n) {
        name = n;
        hours = 0;
    }

    void setHours(int h) {       // 录入计时时长
        hours = h;
    }

    int getHours() const {
        return hours;
    }

    string getName() const {
        return name;
    }

    virtual double getSalary() const = 0;   // 纯虚函数：计算薪酬
    virtual void show() const = 0;          // 纯虚函数：输出信息
    virtual ~Employee() {}
};

// ==================== 高级雇员 ====================
class SeniorEmployee : public Employee {
public:
    SeniorEmployee(string n) : Employee(n) {}

    double getSalary() const override {
        return 5000 + hours * HOURLY_RATE;
    }

    void show() const override {
        cout << "高级雇员 " << name
             << "  固定工资: 5000  计时: " << hours << "h"
             << "  总薪酬: " << getSalary() << "元" << endl;
    }
};

// ==================== 中级雇员 ====================
class MidEmployee : public Employee {
public:
    MidEmployee(string n) : Employee(n) {}

    double getSalary() const override {
        return 4000 + hours * HOURLY_RATE;
    }

    void show() const override {
        cout << "中级雇员 " << name
             << "  固定工资: 4000  计时: " << hours << "h"
             << "  总薪酬: " << getSalary() << "元" << endl;
    }
};

// ==================== 初级雇员 ====================
class JuniorEmployee : public Employee {
public:
    JuniorEmployee(string n) : Employee(n) {}

    double getSalary() const override {
        return 3000 + hours * HOURLY_RATE;
    }

    void show() const override {
        cout << "初级雇员 " << name
             << "  固定工资: 3000  计时: " << hours << "h"
             << "  总薪酬: " << getSalary() << "元" << endl;
    }
};

// ==================== 临时雇员 ====================
class TempEmployee : public Employee {
public:
    TempEmployee(string n) : Employee(n) {}

    double getSalary() const override {
        return hours * HOURLY_RATE;           // 没有固定工资
    }

    void show() const override {
        cout << "临时雇员 " << name
             << "  固定工资: 0  计时: " << hours << "h"
             << "  总薪酬: " << getSalary() << "元" << endl;
    }
};

// ==================== 主函数 ====================
int main() {
    // ① 创建雇员数组（基类指针管理）
    Employee* staff[6];
    staff[0] = new SeniorEmployee("张三");
    staff[1] = new MidEmployee("李四");
    staff[2] = new JuniorEmployee("王五");
    staff[3] = new TempEmployee("赵六");
    staff[4] = new SeniorEmployee("钱七");
    staff[5] = new MidEmployee("孙八");

    // ② 录入本月计时时长
    int testHours[] = { 20, 30, 40, 50, 10, 25 };
    for (int i = 0; i < 6; i++) {
        staff[i]->setHours(testHours[i]);
    }

    // ③ 统一输出薪酬信息
    cout << "========== 本月薪酬报表 ==========" << endl;
    for (int i = 0; i < 6; i++) {
        staff[i]->show();
    }

    // 释放内存
    for (int i = 0; i < 6; i++) {
        delete staff[i];
    }

    return 0;
}
