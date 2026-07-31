#include <iostream>
using namespace std;

class Carlo {
private:
    float weight;    // 本箱重量
    float price;     // 本箱单价
    float value;     // 本箱总价 = weight * price

    static float totalWeight;  // 库存总重量
    static float totalValue;   // 库存总价值

public:
    // 构造函数：购进一箱货物，累加到库存
    Carlo(float w, float p) {
        weight = w;
        price = p;
        value = w * p;
        totalWeight += weight;
        totalValue += value;
        cout << ">>> 购进货箱: 重量=" << weight
             << "kg, 单价=" << price << "元/kg, 价值=" << value << "元" << endl;
        showTotal();
    }

    // 析构函数：卖出/删除货物，从库存中减去
    ~Carlo() {
        totalWeight -= weight;
        totalValue -= value;
        cout << "<<< 卖出货箱: 重量=" << weight
             << "kg, 单价=" << price << "元/kg, 价值=" << value << "元" << endl;
        showTotal();
    }

    // 静态函数：显示当前库存总情况
    static void showTotal() {
        cout << "  [库存] 总重量: " << totalWeight
             << "kg, 总价值: " << totalValue << "元" << endl;
    }

    // 显示本箱信息
    void showBox() {
        cout << "  本箱: 重量=" << weight
             << "kg, 单价=" << price << "元/kg, 价值=" << value << "元" << endl;
    }
};

// 静态数据成员必须在类外初始化
float Carlo::totalWeight = 0;
float Carlo::totalValue = 0;


int main() {
    cout << "========== 商店库存管理系统 ==========" << endl;
    Carlo::showTotal();

    cout << "\n--- 第一次进货：购进 3 箱 ---" << endl;
    Carlo box1(100, 5);    // 100kg，单价5元/kg
    Carlo box2(200, 3);    // 200kg，单价3元/kg
    Carlo box3(150, 4);    // 150kg，单价4元/kg

    cout << "\n--- 卖出 box1（超出作用域自动析构）---" << endl;
    {
        Carlo tempBox(50, 6);   // 临时进一箱 50kg
        // tempBox 离开这个 {} 作用域时自动析构（卖出）
    }  // <-- tempBox 在这里析构

    cout << "\n--- 当前库存中所有货箱信息 ---" << endl;
    box1.showBox();
    box2.showBox();
    box3.showBox();
    Carlo::showTotal();

    cout << "\n--- main 结束，所有库存对象依次销毁 ---" << endl;
    return 0;
}
