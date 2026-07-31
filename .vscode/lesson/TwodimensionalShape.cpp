#include <iostream>
#include <cmath>
using namespace std;

class TwodimensionalShape {
public:
    virtual double getArea() const = 0;

    virtual void show() const {
        cout << "这是一个二维图形" << endl;
    }
};

class Square : public TwodimensionalShape {
private:
    double side;
public:
    Square(double s = 0) {
        side = s;
    }

    double getArea() const override {
        return side * side;
    }

    void show() const override {
        cout << "正方形，边长 = " << side
             << "，面积 = " << getArea() << endl;
    }
};

class Circle : public TwodimensionalShape {
private:
    double radius;
    static constexpr double PI = 3.141592653589;
public:
    Circle(double r = 0) {
        radius = r;
    }

    double getArea() const override {
        return PI * radius * radius;
    }

    void show() const override {
        cout << "圆形，半径 = " << radius
             << "，面积 = " << getArea() << endl;
    }
};

class Triangle : public TwodimensionalShape {
private:
    double side1, side2, side3;
public:
    Triangle(double a = 0, double b = 0, double c = 0) {
        side1 = a;
        side2 = b;
        side3 = c;
    }

    double getArea() const override {
        double s = (side1 + side2 + side3) / 2.0;
        return sqrt(s * (s - side1) * (s - side2) * (s - side3));
    }

    void show() const override {
        cout << "三角形，三边 = " << side1 << ", " << side2 << ", " << side3
             << "，面积 = " << getArea() << endl;
    }
};

int main() {
    TwodimensionalShape* shapes[3];

    shapes[0] = new Square(5.0);
    shapes[1] = new Circle(3.0);
    shapes[2] = new Triangle(3.0, 4.0, 5.0);

    for (int i = 0; i < 3; i++) {
        shapes[i]->show();
    }

    for (int i = 0; i < 3; i++) {
        delete shapes[i];
    }

    return 0;
}
