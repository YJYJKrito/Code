#include <iostream>
#include <string>
using namespace std;

const double HOURLY_RATE = 30;

class Employee {
protected:
    string name;
    int hours;
public:
    Employee(string n) {
        name = n;
        hours = 0;
    }

    void setHours(int h) {
        hours = h;
    }

    virtual double getSalary() const = 0;
    virtual void show() const = 0;
    virtual ~Employee() {}
};

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

class TempEmployee : public Employee {
public:
    TempEmployee(string n) : Employee(n) {}

    double getSalary() const override {
        return hours * HOURLY_RATE;
    }

    void show() const override {
        cout << "临时雇员 " << name
             << "  固定工资: 0  计时: " << hours << "h"
             << "  总薪酬: " << getSalary() << "元" << endl;
    }
};

int main() {
    Employee* staff[6];
    staff[0] = new SeniorEmployee("张三");
    staff[1] = new MidEmployee("李四");
    staff[2] = new JuniorEmployee("王五");
    staff[3] = new TempEmployee("赵六");
    staff[4] = new SeniorEmployee("钱七");
    staff[5] = new MidEmployee("孙八");

    int testHours[] = { 20, 30, 40, 50, 10, 25 };
    for (int i = 0; i < 6; i++) {
        staff[i]->setHours(testHours[i]);
    }

    cout << "========== 本月薪酬报表 ==========" << endl;
    for (int i = 0; i < 6; i++) {
        staff[i]->show();
    }

    for (int i = 0; i < 6; i++) {
        delete staff[i];
    }

    return 0;
}
