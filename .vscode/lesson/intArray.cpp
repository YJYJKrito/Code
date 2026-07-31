#include<iostream>
#include<cstring>

using namespace std;

class intArray{
  protected:                  // ← 改为 protected，派生类才能访问
    int* data;
    int size;

  public:
    intArray(int n){
      size=n;
      data=new int[size];
      for(int i=0;i<size;i++){
        data[i]=0;
      }
    }

    intArray(const intArray& other){
      size=other.size;
      data=new int[size];
      for(int i=0;i<size;i++){
        data[i]=other.data[i];
      }
    }

    ~intArray(){
      delete[]data;
    }

    intArray& operator=(const intArray& other){
      if(this == & other)return* this;
      delete[] data;
      size=other.size;
      data=new int[size];
      for(int i=0;i<size;i++){
        data[i]=other.data[i];
      }
      return* this;
    }

    int& operator[](int index){
      return data[index];
    }

    int& operator[](int index) const{
      return data[index];
    }

    int getsize() const{
      return size;
    }

    void print(){
      for(int i=0;i<size;i++){
        cout<<data[i]<<" ";
      }
      cout<<endl;
    }

    int find(int value)const{
      for(int i=0;i<size;i++){
        if(data[i]==value){
          return i;
        }
      }
      return -1;
    }

    void insert(int index, int value) {
        if (index < 0 || index > size) return;

        int* newData = new int[size + 1];

        for (int i = 0; i < index; i++) {
            newData[i] = data[i];
        }
        newData[index] = value;
        for (int i = index; i < size; i++) {
            newData[i + 1] = data[i];
        }

        delete[] data;
        data = newData;
        size++;
    }

    void remove(int index) {
        if (index < 0 || index >= size) return;
        if (size == 0) return;

        int* newData = new int[size - 1];

        for (int i = 0; i < index; i++) {
            newData[i] = data[i];
        }
        for (int i = index + 1; i < size; i++) {
            newData[i - 1] = data[i];
        }

        delete[] data;
        data = newData;
        size--;
    }
};

class intSet : public intArray {
private:
    int capacity;

public:
    intSet(int cap = 10) : intArray(cap) {
        capacity = cap;
        size = 0;
    }

    intSet(const intSet& other) : intArray(other.capacity) {
        capacity = other.capacity;
        size = other.size;
        for (int i = 0; i < size; i++) {
            data[i] = other.data[i];
        }
    }

    void add(int value) {
        if (find(value) != -1) return;

        if (size >= capacity) {
            int newCap = capacity * 2;
            int* newData = new int[newCap];

            for (int i = 0; i < size; i++) {
                newData[i] = data[i];
            }

            delete[] data;
            data = newData;
            capacity = newCap;
        }

        data[size] = value;
        size++;
    }

    void remove(int value) {
        int idx = find(value);
        if (idx == -1) return;
        intArray::remove(idx);
    }

    intSet operator+(const intSet& other) const {
        intSet result(capacity + other.capacity);  // 预分配足够空间

        for (int i = 0; i < size; i++) {
            result.add(data[i]);
        }
        for (int i = 0; i < other.size; i++) {
            result.add(other.data[i]);
        }

        return result;
    }

    intSet operator*(const intSet& other) const {
        intSet result(capacity < other.capacity ? capacity : other.capacity);

        for (int i = 0; i < size; i++) {
            if (other.find(data[i]) != -1) {
                result.add(data[i]);
            }
        }

        return result;
    }

    int getcapacity() const {
        return capacity;
    }
};

int main() {
    cout << "=== intArray 测试 ===" << endl;
    intArray arr(5);
    arr[0] = 10; arr[1] = 20; arr[2] = 30; arr[3] = 40; arr[4] = 50;
    cout << "原始数组: "; arr.print();

    arr.insert(2, 99);
    cout << "插入 99@2: "; arr.print();

    arr.remove(3);
    cout << "删除下标3: "; arr.print();

    cout << "\n=== intSet 测试 ===" << endl;

    intSet set1;
    set1.add(1);
    set1.add(2);
    set1.add(3);
    set1.add(3);
    set1.add(2);
    cout << "set1: "; set1.print();

    intSet set2;
    set2.add(2);
    set2.add(3);
    set2.add(4);
    cout << "set2: "; set2.print();

    intSet set3 = set1 + set2;
    cout << "set1 + set2 (并集): "; set3.print();

    intSet set4 = set1 * set2;
    cout << "set1 * set2 (交集): "; set4.print();

    set1.remove(2);
    cout << "set1 删除 2 后: "; set1.print();

    cout << "\n扩容测试 (capacity=3):" << endl;
    intSet set5(3);
    cout << "初始容量: " << set5.getcapacity() << endl;
    set5.add(10);
    set5.add(20);
    set5.add(30);
    cout << "加入3个后: "; set5.print();
    cout << "当前容量: " << set5.getcapacity() << endl;

    set5.add(40);
    cout << "加入第4个后: "; set5.print();
    cout << "扩容后容量: " << set5.getcapacity() << endl;  // 3*2 = 6

    return 0;
}
