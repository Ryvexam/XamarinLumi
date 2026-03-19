using System;
using Xamarin.Forms;
using Xamarin.Forms.Shapes;

class Test {
    void Foo() {
        var p = new Path();
        var brush = p.Stroke;
        Console.WriteLine(brush.GetType());
    }
}
