#include <iostream>
#include <string>
using namespace std;

class Publication {
protected:
    string name;
public:
    Publication(string n) : name(n) {}

    virtual void show() const = 0;
    virtual ~Publication() {}
};

class Book : public Publication {
private:
    string author;
public:
    Book(string n, string a) : Publication(n) {
        author = a;
    }

    void show() const override {
        cout << name << "  " << author << endl;
    }
};

class Newspaper : public Publication {
private:
    string publishDate;
public:
    Newspaper(string n, string d) : Publication(n) {
        publishDate = d;
    }

    void show() const override {
        cout << name << "  " << publishDate << endl;
    }
};

class Magazine : public Publication {
private:
    string publishDate;
    int volume;
public:
    Magazine(string n, string d, int v) : Publication(n) {
        publishDate = d;
        volume = v;
    }

    void show() const override {
        cout << name << "  " << publishDate
             << "  volume" << volume << endl;
    }
};

class Library {
    Publication* publications[20];
    static int total;
public:
    void add(Publication& p) {
        if (total < 20) {
            publications[total] = &p;
            total++;
        }
    }

    void show() {
        for (int i = 0; i < total; i++) {
            publications[i]->show();
        }
    }
};

int Library::total = 0;

int main() {
    Book b1("C++", "Nan Jiang");
    Book b2("Data Structure", "Yan Weimin");
    Newspaper n1("People's Daily", "2023/1/1");
    Newspaper n2("China Daily", "2023/6/15");
    Magazine m1("Computer Science", "2023/5/1", 18);
    Magazine m2("Nature", "2023/7/1", 52);

    Library lib;
    lib.add(b1);
    lib.add(b2);
    lib.add(n1);
    lib.add(n2);
    lib.add(m1);
    lib.add(m2);

    lib.show();

    return 0;
}
