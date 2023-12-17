using System;
using System.IO;
using System.Drawing;

class Triangulation
{
    public List<Vector2> points = new List<Vector2>();
    public List<Triangle> triangles = new List<Triangle>();

    private DynamicCache Cache = null;

    public Triangulation(List<Vector2> _points)
    {
        points = _points;

        //Инициализация кэша
        Cache = new DynamicCache(points[2]);

        //Добавление супер структуры
        triangles.Add(new Triangle(points[0], points[1], points[2]));
        triangles.Add(new Triangle(triangles[0].arcs[2], points[3]));

        //Добавление ссылок в ребра на смежные треугольники супер структуры
        triangles[0].arcs[2].trAB = triangles[1];
        triangles[1].arcs[0].trBA = triangles[0];

        //Добавление супер структуры в кэш
        Cache.Add(triangles[0]);
        Cache.Add(triangles[1]);

        Triangle CurentTriangle = null;
        Triangle NewTriangle0 = null;
        Triangle NewTriangle1 = null;
        Triangle NewTriangle2 = null;

        Arc NewArc0 = null;
        Arc NewArc1 = null;
        Arc NewArc2 = null;

        Arc OldArc0 = null;
        Arc OldArc1 = null;
        Arc OldArc2 = null;

        for (int i = 4; i < _points.Count; i++)
        {
            CurentTriangle = GetTriangleForPoint(_points[i]);

            if (CurentTriangle != null)
            {
                //Создание новых ребер, которые совместно с ребрами преобразуемого треугольника образуют новые три треугольника 
                NewArc0 = new Arc(CurentTriangle.points[0], _points[i]);
                NewArc1 = new Arc(CurentTriangle.points[1], _points[i]);
                NewArc2 = new Arc(CurentTriangle.points[2], _points[i]);

                //Сохранение ребер преобразуемого треугольника
                OldArc0 = CurentTriangle.GetArcBeatwen2Points(CurentTriangle.points[0], CurentTriangle.points[1]);
                OldArc1 = CurentTriangle.GetArcBeatwen2Points(CurentTriangle.points[1], CurentTriangle.points[2]);
                OldArc2 = CurentTriangle.GetArcBeatwen2Points(CurentTriangle.points[2], CurentTriangle.points[0]);

                //Преобразование текущего треугольника в один из новых трех
                NewTriangle0 = CurentTriangle;
                NewTriangle0.arcs[0] = OldArc0;
                NewTriangle0.arcs[1] = NewArc1;
                NewTriangle0.arcs[2] = NewArc0;
                NewTriangle0.points[2] = _points[i];

                //Дополнительно создаются два треугольника
                NewTriangle1 = new Triangle(OldArc1, NewArc2, NewArc1);
                NewTriangle2 = new Triangle(OldArc2, NewArc0, NewArc2);

                //Новым ребрам передаются ссылки на образующие их треугольники
                NewArc0.trAB = NewTriangle0;
                NewArc0.trBA = NewTriangle2;
                NewArc1.trAB = NewTriangle1;
                NewArc1.trBA = NewTriangle0;
                NewArc2.trAB = NewTriangle2;
                NewArc2.trBA = NewTriangle1;

                //Передача ссылок на старые ребра
                if (OldArc0.trAB == CurentTriangle)
                    OldArc0.trAB = NewTriangle0;
                if (OldArc0.trBA == CurentTriangle)
                    OldArc0.trBA = NewTriangle0;

                if (OldArc1.trAB == CurentTriangle)
                    OldArc1.trAB = NewTriangle1;
                if (OldArc1.trBA == CurentTriangle)
                    OldArc1.trBA = NewTriangle1;

                if (OldArc2.trAB == CurentTriangle)
                    OldArc2.trAB = NewTriangle2;
                if (OldArc2.trBA == CurentTriangle)
                    OldArc2.trBA = NewTriangle2;


                triangles.Add(NewTriangle1);
                triangles.Add(NewTriangle2);

                Cache.Add(NewTriangle0);
                Cache.Add(NewTriangle1);
                Cache.Add(NewTriangle2);

                CheckDelaunayAndRebuild(OldArc0);
                CheckDelaunayAndRebuild(OldArc1);
                CheckDelaunayAndRebuild(OldArc2);
            }

        }

        //Дополнительный проход
        for (int i = 0; i < triangles.Count; i++)
        {
            CheckDelaunayAndRebuild(triangles[i].arcs[0]);
            CheckDelaunayAndRebuild(triangles[i].arcs[1]);
            CheckDelaunayAndRebuild(triangles[i].arcs[2]);
        }
    }

    //Возвращает треугольник в котором находится данная точка
    private Triangle GetTriangleForPoint(Vector2 _point)
    {
        Triangle link = Cache.FindTriangle(_point);

        if (link == null)
        {
            link = triangles[0];
        }

        if (IsPointInTriangle(link, _point))
        {
            return link;
        }
        else
        {
            //Путь от некоторого треугольника до искомой точки
            Arc way = new Arc(_point, link.Centroid);
            Arc CurentArc = null;

            while (!IsPointInTriangle(link, _point))
            {
                CurentArc = GetIntersectedArc(way, link);
                if (link != CurentArc.trAB)
                    link = CurentArc.trAB;
                else
                    link = CurentArc.trBA;

                way = new Arc(_point, link.Centroid);
            }
            return link;

        }
    }

    //Возвращает ребро треугольника которое пересекается с линией
    private Arc GetIntersectedArc(Arc Line, Triangle Target)
    {
        if (Arc.ArcIntersect(Target.arcs[0], Line))
            return Target.arcs[0];
        if (Arc.ArcIntersect(Target.arcs[1], Line))
            return Target.arcs[1];
        if (Arc.ArcIntersect(Target.arcs[2], Line))
            return Target.arcs[2];

        return null;
    }

    private bool IsPointInTriangle(Triangle _triangle, Vector2 _point)
    {
        Vector2 P1 = _triangle.points[0];
        Vector2 P2 = _triangle.points[1];
        Vector2 P3 = _triangle.points[2];
        Vector2 P4 = _point;

        double a = (P1.x - P4.x) * (P2.y - P1.y) - (P2.x - P1.x) * (P1.y - P4.y);
        double b = (P2.x - P4.x) * (P3.y - P2.y) - (P3.x - P2.x) * (P2.y - P4.y);
        double c = (P3.x - P4.x) * (P1.y - P3.y) - (P1.x - P3.x) * (P3.y - P4.y);

        if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0))
            return true;
        else
            return false;
    }

    //Вычисление критерия Делоне по описанной окружности
    private bool IsDelaunay(Vector2 A, Vector2 B, Vector2 C, Vector2 _CheckNode)
    {
        double x0 = _CheckNode.x;
        double y0 = _CheckNode.y;
        double x1 = A.x;
        double y1 = A.y;
        double x2 = B.x;
        double y2 = B.y;
        double x3 = C.x;
        double y3 = C.y;

        double[] matrix = { (x1 - x0)*(x1 - x0) + (y1 - y0)*(y1 - y0), x1 - x0, y1 - y0,
                                 (x2 - x0)*(x2 - x0) + (y2 - y0)*(y2 - y0), x2 - x0, y2 - y0,
                                 (x3 - x0)*(x3 - x0) + (y3 - y0)*(y3 - y0), x3 - x0, y3 - y0};

        double matrixDeterminant = matrix[0] * matrix[4] * matrix[8] + matrix[1] * matrix[5] * matrix[6] + matrix[2] * matrix[3] * matrix[7] -
                                    matrix[2] * matrix[4] * matrix[6] - matrix[0] * matrix[5] * matrix[7] - matrix[1] * matrix[3] * matrix[8];

        double a = x1 * y2 * 1 + y1 * 1 * x3 + 1 * x2 * y3
                 - 1 * y2 * x3 - y1 * x2 * 1 - 1 * y3 * x1;

        //Sgn(a)
        if (a < 0)
            matrixDeterminant *= -1d;

        if (matrixDeterminant < 0d)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void CheckDelaunayAndRebuild(Arc arc)
    {
        Triangle T1 = null;
        Triangle T2 = null;

        if (arc.trAB != null && arc.trBA != null)
        {
            T1 = arc.trAB;
            T2 = arc.trBA;
        }
        else
            return;

        Vector2[] CurentPoints = new Vector2[4];

        Arc OldArcT1A1 = null;
        Arc OldArcT1A2 = null;
        Arc OldArcT2A1 = null;
        Arc OldArcT2A2 = null;

        Arc NewArcT1A1 = null;
        Arc NewArcT1A2 = null;
        Arc NewArcT2A1 = null;
        Arc NewArcT2A2 = null;

        CurentPoints[0] = T1.GetThirdPoint(arc);
        CurentPoints[1] = arc.A;
        CurentPoints[2] = arc.B;
        CurentPoints[3] = T2.GetThirdPoint(arc);

        //Дополнительная проверка, увеличивает скорость алгоритма на 10%
        if (Arc.ArcIntersect(CurentPoints[0], CurentPoints[3], CurentPoints[1], CurentPoints[2]))
            if (!IsDelaunay(CurentPoints[0], CurentPoints[1], CurentPoints[2], CurentPoints[3]))
            {

                T1.GetTwoOtherArcs(arc, out OldArcT1A1, out OldArcT1A2);
                T2.GetTwoOtherArcs(arc, out OldArcT2A1, out OldArcT2A2);

                if (OldArcT1A1.IsConnectedWith(OldArcT2A1))
                {
                    NewArcT1A1 = OldArcT1A1; NewArcT1A2 = OldArcT2A1;
                    NewArcT2A1 = OldArcT1A2; NewArcT2A2 = OldArcT2A2;
                }
                else
                {
                    NewArcT1A1 = OldArcT1A1; NewArcT1A2 = OldArcT2A2;
                    NewArcT2A1 = OldArcT1A2; NewArcT2A2 = OldArcT2A1;
                }

                //Изменение ребра
                arc.A = CurentPoints[0];
                arc.B = CurentPoints[3];

                //переопределение ребер треугольников
                T1.arcs[0] = arc;
                T1.arcs[1] = NewArcT1A1;
                T1.arcs[2] = NewArcT1A2;

                T2.arcs[0] = arc;
                T2.arcs[1] = NewArcT2A1;
                T2.arcs[2] = NewArcT2A2;

                //перезапись точек треугольников
                T1.points[0] = arc.A;
                T1.points[1] = arc.B;
                T1.points[2] = Arc.GetCommonPoint(NewArcT1A1, NewArcT1A2);

                T2.points[0] = arc.A;
                T2.points[1] = arc.B;
                T2.points[2] = Arc.GetCommonPoint(NewArcT2A1, NewArcT2A2);

                //Переопределение ссылок в ребрах
                if (NewArcT1A2.trAB == T2)
                    NewArcT1A2.trAB = T1;
                else if (NewArcT1A2.trBA == T2)
                    NewArcT1A2.trBA = T1;

                if (NewArcT2A1.trAB == T1)
                    NewArcT2A1.trAB = T2;
                else if (NewArcT2A1.trBA == T1)
                    NewArcT2A1.trBA = T2;

                //Добавление треугольников в кэш
                Cache.Add(T1);
                Cache.Add(T2);

            }
    }
}

public class Vector2
{
    public double x;
    public double y;


    public Vector2(double _x, double _y)
    {
        x = _x;
        y = _y;
    }

    public static Vector2 operator -(Vector2 _a, Vector2 _b)
    {
        return new Vector2(_a.x - _b.x, _a.y - _b.y);
    }

    public static Vector2 operator +(Vector2 _a, Vector2 _b)
    {
        return new Vector2(_a.x + _b.x, _a.y + _b.y);
    }

    public static Vector2 operator *(Vector2 _a, double s)
    {
        return new Vector2(_a.x * s, _a.y * s);
    }

    public static double CrossProduct(Vector2 v1, Vector2 v2) //Векторное произведение
    {
        return v1.x * v2.y - v2.x * v1.y;
    }

}

public class Triangle
{
    public Vector2[] points = new Vector2[3];
    public Arc[] arcs = new Arc[3];

    public Vector2 Centroid
    {
        get
        {
            return points[2] - ((points[2] - (points[0] + ((points[1] - points[0]) * 0.5))) * 0.6666666);
        }

        set
        { }
    }

    public System.Drawing.Color color;

    public Triangle(Vector2 _a, Vector2 _b, Vector2 _c)
    {
        points[0] = _a;
        points[1] = _b;
        points[2] = _c;

        arcs[0] = new Arc(_a, _b);
        arcs[1] = new Arc(_b, _c);
        arcs[2] = new Arc(_c, _a);
    }

    public Triangle(Arc _arc, Vector2 _a)
    {
        points[0] = _arc.A;
        points[1] = _arc.B;
        points[2] = _a;

        arcs[0] = _arc;
        arcs[1] = new Arc(points[1], points[2]);
        arcs[2] = new Arc(points[2], points[0]);
    }

    public Triangle(Arc _arc0, Arc _arc1, Arc _arc2)
    {
        arcs[0] = _arc0;
        arcs[1] = _arc1;
        arcs[2] = _arc2;

        points[0] = _arc0.A;
        points[1] = _arc0.B;

        if (_arc1.A == _arc0.A || _arc1.A == _arc0.B)
            points[2] = _arc1.B;
        else if (_arc1.B == _arc0.A || _arc1.B == _arc0.B)
            points[2] = _arc1.A;
        else if (points[2] != _arc2.A && points[2] != _arc2.B)
        {
            Console.WriteLine("ARC0.A: " + _arc0.A.x + " " + _arc0.A.y);
            Console.WriteLine("ARC0.B: " + _arc0.B.x + " " + _arc0.B.y);
            Console.WriteLine("ARC1.A: " + _arc1.A.x + " " + _arc1.A.y);
            Console.WriteLine("ARC1.B: " + _arc1.B.x + " " + _arc1.B.y);
            Console.WriteLine("ARC2.A: " + _arc2.A.x + " " + _arc2.A.y);
            Console.WriteLine("ARC2.B: " + _arc2.B.x + " " + _arc2.B.y);

            throw new Exception("Попытка создать треугольник из трех непересекающихся ребер");
        }

    }

    public Vector2 GetThirdPoint(Arc _arc)
    {
        for (int i = 0; i < 3; i++)
            if (_arc.A != points[i] && _arc.B != points[i])
                return points[i];

        return null;
    }

    public Arc GetArcBeatwen2Points(Vector2 _a, Vector2 _b)
    {
        for (int i = 0; i < 3; i++)
            if (arcs[i].A == _a && arcs[i].B == _b || arcs[i].A == _b && arcs[i].B == _a)
                return arcs[i];

        return null;
    }

    public void GetTwoOtherArcs(Arc _a0, out Arc _a1, out Arc _a2)
    {
        if (arcs[0] == _a0)
        { _a1 = arcs[1]; _a2 = arcs[2]; }
        else if (arcs[1] == _a0)
        { _a1 = arcs[0]; _a2 = arcs[2]; }
        else if (arcs[2] == _a0)
        { _a1 = arcs[0]; _a2 = arcs[1]; }
        else
        { _a1 = null; _a2 = null; }
    }

}

class DynamicCache
{
    private Triangle[] Cache = new Triangle[4];

    //Текущий размер кэша
    private UInt32 Size = 2;

    //Треугольников в кэше
    private UInt32 InCache = 0;

    //Реальные размеры кэшируемого пространства
    private Vector2 SizeOfSpace;

    //Размеры одной ячейки кэша в пересчете на реальное пространство
    private double xSize;
    private double ySize;

    public DynamicCache(Vector2 _sizeOfSpace)
    {
        SizeOfSpace = _sizeOfSpace;
        xSize = SizeOfSpace.x / (double)Size;
        ySize = SizeOfSpace.y / (double)Size;
    }

    public void Add(Triangle _T)
    {
        InCache++;

        if (InCache >= Cache.Length * 3)
            Increase();

        Cache[GetKey(_T.Centroid)] = _T;
    }

    public Triangle FindTriangle(Vector2 _Point)
    {
        UInt32 key = GetKey(_Point);
        if (Cache[key] != null)
            return Cache[key];

        // Дополнительный поиск не null ячейки, ускоряет алгоритм 
        for (uint i = key - 25; i < key && i >= 0 && i < Cache.Length; i++)
            if (Cache[i] != null)
                return Cache[i];

        for (uint i = key + 25; i > key && i >= 0 && i < Cache.Length; i--)
            if (Cache[i] != null)
                return Cache[i];

        return null;
    }

    //Увеличивает размер кэша в 4 раза
    private void Increase()
    {
        Triangle[] NewCache = new Triangle[(Size * 2) * (Size * 2)];
        UInt32 newIndex = 0;

        //Передача ссылок из старого кэша в новый
        for (UInt32 i = 0; i < Cache.Length; i++)
        {
            newIndex = GetNewIndex(i);
            NewCache[newIndex] = Cache[i];
            NewCache[newIndex + 1] = Cache[i];
            NewCache[newIndex + Size * 2] = Cache[i];
            NewCache[newIndex + Size * 2 + 1] = Cache[i];
        }

        Size = Size * 2;
        xSize = SizeOfSpace.x / (double)Size;
        ySize = SizeOfSpace.y / (double)Size;

        Cache = NewCache;
    }

    private UInt32 GetKey(Vector2 _point)
    {
        UInt32 i = (UInt32)(_point.y / ySize);
        UInt32 j = (UInt32)(_point.x / xSize);

        if (i == Size)
            i--;
        if (j == Size)
            j--;

        return i * Size + j;
    }

    private UInt32 GetNewIndex(UInt32 _OldIndex)
    {
        UInt32 i = (_OldIndex / Size) * 2;
        UInt32 j = (_OldIndex % Size) * 2;

        return i * (Size * 2) + j;
    }
}

public class Arc //Ребро
{

    public Vector2 A;
    public Vector2 B;

    //Ссылка на треугольники в которые входит ребро
    public Triangle trAB;
    public Triangle trBA;

    //Ребро является границей триангуляции если не ссылается на 2 треугольника
    public bool IsBorder
    {
        get
        {
            if (trAB == null || trBA == null)
                return true;
            else
                return false;
        }
        set { }
    }

    public Arc(Vector2 _A, Vector2 _B)
    {
        A = _A;
        B = _B;
    }

    public static bool ArcIntersect(Arc a1, Arc a2)
    {
        Vector2 p1, p2, p3, p4;
        p1 = a1.A;
        p2 = a1.B;
        p3 = a2.A;
        p4 = a2.B;

        //Перепроверить CrossProduct
        double d1 = Direction(p3, p4, p1);
        double d2 = Direction(p3, p4, p2);
        double d3 = Direction(p1, p2, p3);
        double d4 = Direction(p1, p2, p4);

        if (p1 == p3 || p1 == p4 || p2 == p3 || p2 == p4)
            return false;
        else if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &
                 ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
        else if ((d1 == 0) && OnSegment(p3, p4, p1))
            return true;
        else if ((d2 == 0) && OnSegment(p3, p4, p2))
            return true;
        else if ((d3 == 0) && OnSegment(p1, p2, p3))
            return true;
        else if ((d4 == 0) && OnSegment(p1, p2, p4))
            return true;
        else
            return false;
    }

    public static bool ArcIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {

        //Перепроверить CrossProduct
        double d1 = Direction(p3, p4, p1);
        double d2 = Direction(p3, p4, p2);
        double d3 = Direction(p1, p2, p3);
        double d4 = Direction(p1, p2, p4);

        if (p1 == p3 || p1 == p4 || p2 == p3 || p2 == p4)
            return false;
        else if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &
                 ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
        else if ((d1 == 0) && OnSegment(p3, p4, p1))
            return true;
        else if ((d2 == 0) && OnSegment(p3, p4, p2))
            return true;
        else if ((d3 == 0) && OnSegment(p1, p2, p3))
            return true;
        else if ((d4 == 0) && OnSegment(p1, p2, p4))
            return true;
        else
            return false;
    }

    public static Vector2 GetCommonPoint(Arc a1, Arc a2)
    {
        if (a1.A == a2.A)
            return a1.A;
        else if (a1.A == a2.B)
            return a1.A;
        else if (a1.B == a2.A)
            return a1.B;
        else if (a1.B == a2.B)
            return a1.B;
        else
            return null;
    }

    //Определяет, связаны ли ребра
    public bool IsConnectedWith(Arc _a)
    {
        if (A == _a.A || A == _a.B)
            return true;

        if (B == _a.A || B == _a.B)
            return true;

        return false;
    }

    private static double Direction(Vector2 pi, Vector2 pj, Vector2 pk)
    {
        return Vector2.CrossProduct((pk - pi), (pj - pi));
    }
    private static bool OnSegment(Vector2 pi, Vector2 pj, Vector2 pk)
    {
        if ((Math.Min(pi.x, pj.x) <= pk.x && pk.x <= Math.Max(pi.x, pj.x)) && (Math.Min(pi.y, pj.y) <= pk.y && pk.y <= Math.Max(pi.y, pj.y)))
            return true;
        else
            return false;
    }



}
class Program
{
    static void Main()
    {
        // Загрузка изображения
        Bitmap image = new Bitmap("InterlacedImage.jpg"); // Замените путь на путь к вашему изображению

        // Нахождение координат черных пикселей и их загрузка в массив
        int[,] array = GetBlackPixelCoordinates(image);

        // Вывод координат черных пикселей
        Console.WriteLine("Black Pixel Coordinates:");
        for (int i = 0; i < array.GetLength(0); i++)
        {
            Console.WriteLine($"X: {array[i, 0]}, Y: {array[i, 1]}");
        }

        // Создание списка точек для триангуляции
        List<Vector2> Points = new List<Vector2>();

        // Добавление "рамочных" точек
        Points.Add(new Vector2(0, 0));
        Points.Add(new Vector2(image.Width, 0));
        Points.Add(new Vector2(image.Width, image.Height));
        Points.Add(new Vector2(0, image.Height));

        // Добавление точек из массива в список
        for (int i = 0; i < array.GetLength(0); i++)
        {
            Points.Add(new Vector2(array[i, 0], array[i, 1]));
        }

        // Создание объекта триангуляции
        Triangulation triangulation = new Triangulation(Points);

        // Рисование границ треугольников на изображении
        using (Graphics g = Graphics.FromImage(image))
        {
            Pen pen = new Pen(Color.Red); // Цвет границы треугольников
            foreach (var triangle in triangulation.triangles)
            {
                g.DrawLine(pen, (float)triangle.points[0].x, (float)triangle.points[0].y, (float)triangle.points[1].x, (float)triangle.points[1].y);
                g.DrawLine(pen, (float)triangle.points[1].x, (float)triangle.points[1].y, (float)triangle.points[2].x, (float)triangle.points[2].y);
                g.DrawLine(pen, (float)triangle.points[2].x, (float)triangle.points[2].y, (float)triangle.points[0].x, (float)triangle.points[0].y);
            }
        }

        // Сохранение результата
        image.Save("TriangulatedImage.jpg");

        static int[,] GetBlackPixelCoordinates(Bitmap image)
        {
            int[,] array = new int[image.Width * image.Height, 2];
            int index = 0;

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixelColor = image.GetPixel(x, y);
                    // Проверка на черный цвет
                    if (pixelColor.R == 0 && pixelColor.G == 0 && pixelColor.B == 0)
                    {
                        array[index, 0] = x;
                        array[index, 1] = y;
                        index++;
                    }
                }
            }

            // Уменьшение размера массива до активных элементов
            int[,] resultArray = new int[index, 2];
            Array.Copy(array, resultArray, index * 2);

            return resultArray;
        }
    }
}
/*
class Program
{
    static void Main()
    {
        Console.WriteLine("Введите путь к изображению:");
        string imagePath = Console.ReadLine();

        if (File.Exists(imagePath))
        {
            DisplayImage(imagePath);
        }
        else
        {
            Console.WriteLine("Файл не найден.");
        }
    }

    static void DisplayImage(string imagePath)
    {
        using (var image = Image.Load<Rgba32>(imagePath))
        {

            ApplyInterlace(image);

            // Сохранение изображения
            string outputImagePath = "InterlacedImage.jpg";
            image.Save(outputImagePath);
            Console.WriteLine($"Изображение сохранено по пути: {outputImagePath}");
        }

        Console.ReadLine(); // Добавим задержку, чтобы консоль не закрылась сразу
    }

    static void ApplyInterlace(Image<Rgba32> image)
    {
        // Пример простого интерлейса - замена каждого второго пикселя на черный
        for (int y = 1; y < image.Height; y += 2)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0); // Черный цвет
            }
        }
    }
}
*/