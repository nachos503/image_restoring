using System;
using System.IO;
using System.Drawing;
using Color = System.Drawing.Color;
using Brush = System.Drawing.Brush;
using Point = System.Drawing.Point;
using Graphics = System.Drawing.Graphics;
using Rectangle = System.Drawing.Rectangle;

// Triangulation - класс дял построения триангуляции на изображении
class Triangulation
{
    // Список точек, на основе которых строятся треугольники
    public List<ToolVector> points = new List<ToolVector>();
    // Список треугольников
    public List<Triangle> triangles = new List<Triangle>();

    private DynamicCache Cache = null;

    // Triangulation - очень странный и ебанутый конструктор 
    public Triangulation(List<ToolVector> _points)
    {
        points = _points;

        // Инициализация кэша
        Cache = new DynamicCache(points[2]);

        // Добавление супер структуры (что бы то ни значило) (скорей всего это ничего не значит кто бы что не говорил)
        // По сути здесь добавялется в лист один треугольник по трем точкам и к нему добавляется по смежному ребру и третьей точке вторйо треугольник
        // По сути так называемая супер структура - два смежных треугольника
        triangles.Add(new Triangle(points[0], points[1], points[2]));
        triangles.Add(new Triangle(triangles[0].arcs[2], points[3]));

        // Добавление ссылок в ребра на смежные треугольники супер структуры
        triangles[0].arcs[2].trAB = triangles[1];
        triangles[1].arcs[0].trBA = triangles[0];

        // Добавление супер структуры в кэш
        // Добавление двух смежных треугольников в кэш
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

        // Проход по всем данным точкам
        for (int i = 4; i < _points.Count; i++)
        {
            // текущему треугольнику присваивается тот треугольник, в котором находится текущая точка
            CurentTriangle = GetTriangleForPoint(_points[i]);

            // Если текущий треугольник существует
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

                // Добавление в список новых треугольников
                triangles.Add(NewTriangle1);
                triangles.Add(NewTriangle2);

                //Добавление в кэш новых треугольников
                Cache.Add(NewTriangle0);
                Cache.Add(NewTriangle1);
                Cache.Add(NewTriangle2);

                CheckDelaunayAndRebuild(OldArc0);
                CheckDelaunayAndRebuild(OldArc1);
                CheckDelaunayAndRebuild(OldArc2);
            }
        }

        //Дополнительный проход для проверки на критерий Делоне
        for (int i = 0; i < triangles.Count; i++)
        {
            CheckDelaunayAndRebuild(triangles[i].arcs[0]);
            CheckDelaunayAndRebuild(triangles[i].arcs[1]);
            CheckDelaunayAndRebuild(triangles[i].arcs[2]);
        }
    }

    // GetTriangleForPoint - метод, возвращающий треугольник в котором находится данная точка
    private Triangle GetTriangleForPoint(ToolVector _point)
    {
        // link - передача ссылки из кэша
        Triangle link = Cache.FindTriangle(_point);
        // если ссылка пустая - возврат первого треугольника
        if (link == null)
        {
            link = triangles[0];
        }
        // если по ссылке передали верный треугольник - возврат ссылки на треугольник
        if (IsPointInTriangle(link, _point))
        {
            return link;
        }
        // если найденный треугольник не подошел
        else
        {
            //Путь от центроида найденного треугольника до искомой точки
            Arc wayToTriangle = new Arc(_point, link.Centroid);
            Arc CurentArc = null;
            // Пока точка не окажется внутри треугольника
            while (!IsPointInTriangle(link, _point))
            {
                // находим ребро, которое пересекается с найденным треугольником и некоторой прямой от искомой точки
                CurentArc = GetIntersectedArc(wayToTriangle, link);

                // присваиваем треугольник, в которое входит это ребро
                if (link != CurentArc.trAB)
                    link = CurentArc.trAB;
                else
                    link = CurentArc.trBA;

                // если треугольник не найден, то переопределяем путь от точки до центроида нвоого треугольника
                wayToTriangle = new Arc(_point, link.Centroid);
            }
            // Возврат ссылки на треугольник
            return link;
        }
    }

    // GetIntersectedArc - метод, возвращающий ребро треугольника которое пересекается с линией
    private Arc GetIntersectedArc(Arc line, Triangle triangle)
    {
        if (Arc.ArcIntersect(triangle.arcs[0], line))
            return triangle.arcs[0];

        else if (Arc.ArcIntersect(triangle.arcs[1], line))
            return triangle.arcs[1];

        else if (Arc.ArcIntersect(triangle.arcs[2], line))
            return triangle.arcs[2];

        else
            return null;
    }

    // IsPointInTriangle - метод, возвращающий true если заданная точка находится в заданном треугольнике
    private bool IsPointInTriangle(Triangle _triangle, ToolVector _point)
    {
        // Для удобства присвоим всем точкам треугольника переменные
        ToolVector P1 = _triangle.points[0];
        ToolVector P2 = _triangle.points[1];
        ToolVector P3 = _triangle.points[2];
        ToolVector P4 = _point;

        /* Формула вычисляет определитель трех 2x2 матриц, образованных путем вычитания координат x и y точек
            a представляет определитель матрицы, образованной путем вычитания координат x и y точки P4 из P1 и P2 соответственно.
            b представляет определитель матрицы, образованной путем вычитания координат x и y точки P4 из P2 и P3 соответственно.
            c представляет определитель матрицы, образованной путем вычитания координат x и y точки P4 из P3 и P1 соответственно.
           Эта формула происходит из концепции барицентрических координат и широко используется в вычислительной геометрии для определения положения точки относительно многоугольника */
        double a = (P1.x - P4.x) * (P2.y - P1.y) - (P2.x - P1.x) * (P1.y - P4.y);
        double b = (P2.x - P4.x) * (P3.y - P2.y) - (P3.x - P2.x) * (P2.y - P4.y);
        double c = (P3.x - P4.x) * (P1.y - P3.y) - (P1.x - P3.x) * (P3.y - P4.y);

        /* Знак результирующих значений a, b и c может использоваться для определения ориентации точки P4 относительно треугольника:
            Если a, b и c все положительные или все отрицательные, то P4 находится внутри треугольника.
            Если любое из значений a, b или c равно нулю, то P4 находится на одной из сторон треугольника.
            Если a, b и c имеют разные знаки, то P4 находится вне треугольника.
        */
        if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0))
            return true;
        else
            return false;
    }

    //IsDelaunay - метод, вычисляющий принадлежность к критерию Делоне по описанной окружности
    //РАЗОБРАТЬ ПОТОМ ПОТОМУ ЧТО ЗДЕСЬ ОТКРОВЕННОЕ НЕПОНЯТНОЕ ДЕРЬМО
    private bool IsDelaunay(ToolVector A, ToolVector B, ToolVector C, ToolVector _CheckNode)
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

    //CheckDelaunayAndRebuild - метод который тожепроверяет принадлежность к критерию и перестраивает треугольник
    //АНАЛОГИЧНО
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

        ToolVector[] CurentPoints = new ToolVector[4];

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



// ToolVector - вспомогательный класс для работы с координатами точек
// ПЕРЕИМЕНОВАТЬ В КАКОЙ-НИБУДЬ POINTS И НЕ ПОЗОРИТСЯ 
public class ToolVector
{
    // координаты точек
    public double x;
    public double y;

    //конструктор
    public ToolVector(double _x, double _y)
    {
        x = _x;
        y = _y;
    }

    // Переписанные операторы для работы с ToolVector
    public static ToolVector operator -(ToolVector _a, ToolVector _b)
    {
        return new ToolVector(_a.x - _b.x, _a.y - _b.y);
    }
    public static ToolVector operator +(ToolVector _a, ToolVector _b)
    {
        return new ToolVector(_a.x + _b.x, _a.y + _b.y);
    }
    public static ToolVector operator *(ToolVector _a, double s)
    {
        return new ToolVector(_a.x * s, _a.y * s);
    }

    // Векторное произведение
    public static double CrossProduct(ToolVector v1, ToolVector v2) 
    {
        return v1.x * v2.y - v2.x * v1.y;
    }

}



// Triangle - класс для построения треугольников
public class Triangle
{
    // точки образающие треугольник
    public ToolVector[] points = new ToolVector[3];
    //ребра треугольника
    public Arc[] arcs = new Arc[3];
    //какой-то цвет для картинки
    public System.Drawing.Color color;

    // Centroid - метод возвращающйи точку пересечения медиан треугольника (центроид)
    public ToolVector Centroid
    {
        /*
         * points[0] и points[1] представляют первые две вершины треугольника.
            вычисляет вектор от первой вершины ко второй вершин
            вычисляет половину вектора между первой и второй вершинами
            вычисляет середину между первой и второй вершинами
            вычисляет вектор от середины к третьей вершине
            масштабирует вектор на 0.6666666 (приблизительно 2/3)
            вычитает масштабированный вектор из третьей вершины, получая центроид
         */
        get
        {
            return points[2] - ((points[2] - (points[0] + ((points[1] - points[0]) * 0.5))) * 0.6666666);
        }

        // свойство доступно только для чтения
        set { }
    }

    //Построение треугольника по трем точкам
    public Triangle(ToolVector _a, ToolVector _b, ToolVector _c)
    {
        points[0] = _a;
        points[1] = _b;
        points[2] = _c;

        arcs[0] = new Arc(_a, _b);
        arcs[1] = new Arc(_b, _c);
        arcs[2] = new Arc(_c, _a);
    }
    
    // Построение треугольника по ребру и точке
    public Triangle(Arc _arc, ToolVector _a)
    {
        points[0] = _arc.A;
        points[1] = _arc.B;
        points[2] = _a;

        arcs[0] = _arc;
        arcs[1] = new Arc(points[1], points[2]);
        arcs[2] = new Arc(points[2], points[0]);
    }
    
    // Построение треугольника по трем ребрам
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

    //GetThirdPoint - метод получения третий точки треугольника, зная ребро
    public ToolVector GetThirdPoint(Arc _arc)
    {
        for (int i = 0; i < 3; i++)
            if (_arc.A != points[i] && _arc.B != points[i])
                return points[i];

        return null;
    }

    //GetArcBeatwen2Points - метод поиска ребра по двум заданным точкам
    public Arc GetArcBeatwen2Points(ToolVector _a, ToolVector _b)
    {
        for (int i = 0; i < 3; i++)
            if (arcs[i].A == _a && arcs[i].B == _b || arcs[i].A == _b && arcs[i].B == _a)
                return arcs[i];

        return null;
    }

    //GetTwoOtherArcs - метод поиска всех ребер по одному заданному
    public void GetTwoOtherArcs(Arc _a0, out Arc _a1, out Arc _a2)
    {
        //ну тупой перебор епта
        if (arcs[0] == _a0)
        { 
            _a1 = arcs[1];
            _a2 = arcs[2];
        }

        else if (arcs[1] == _a0)
        {
            _a1 = arcs[0];
            _a2 = arcs[2];
        }

        else if (arcs[2] == _a0)
        {
            _a1 = arcs[0];
            _a2 = arcs[1];
        }

        else
        {
            _a1 = null;
            _a2 = null;
        }
    }
}



// DynamicCache - класс для кэша (сказали надо) (не хочу разбираться зачем и что он делает)
class DynamicCache
{
    private Triangle[] Cache = new Triangle[4];

    //Текущий размер кэша
    private UInt32 Size = 2;

    //Треугольников в кэше
    private UInt32 InCache = 0;

    //Реальные размеры кэшируемого пространства
    private ToolVector SizeOfSpace;

    //Размеры одной ячейки кэша в пересчете на реальное пространство
    private double xSize;
    private double ySize;

    public DynamicCache(ToolVector _sizeOfSpace)
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
    public Triangle FindTriangle(ToolVector _Point)
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
    private UInt32 GetKey(ToolVector _point)
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



// Arc - класс для построения ребер
public class Arc
{
    // точки конца ребра
    public ToolVector A;
    public ToolVector B;

    //Ссылка на треугольники в которые входит ребро
    public Triangle trAB;
    public Triangle trBA;

    //Ребро является границей триангуляции если не ссылается на 2 треугольника
    //ЧТО ЭТО ПРОГРАММНО МАТЕМАТИЧЕСКОЕ НЕДОРАЗУМЕНИЕ ЗНАЧИТ ВООБЩЕ И НУЖНО ЛИ ОНО НАМ ЕСЛИ НА НЕГО НИЧЕГО НЕ ССЫЛАЕТСЯ
    public bool IsBorder
    {
        get
        {
            if (trAB == null || trBA == null)
                return true;
            else
                return false;
        }
        // свойство доступно только для чтения
        set { }
    }
    
    //конструктор
    public Arc(ToolVector _A, ToolVector _B)
    {
        A = _A;
        B = _B;
    }

    // ArcIntersect - метод, возвращающий true усли два отрезка пересекаются
    public static bool ArcIntersect(Arc a1, Arc a2)
    {
        //обозначим для удобности точки концов отрезков
        ToolVector p1, p2, p3, p4;
        p1 = a1.A;
        p2 = a1.B;
        p3 = a2.A;
        p4 = a2.B;

        //определение направления
        //ХУЙ ЗНАЕТ ЗАЧЕМ НАДО ГЕОМЕТРИЮ ПЕРЕЧИТАТЬ
        double d1 = Direction(p3, p4, p1);
        double d2 = Direction(p3, p4, p2);
        double d3 = Direction(p1, p2, p3);
        double d4 = Direction(p1, p2, p4);

        /*
         Векторное произведение этих двух векторов дает значение, которое указывает направление или ориентацию трех точек. Знак результата определяет, расположены ли точки по часовой стрелке или против часовой стрелки.
            Если результат положительный, то точки расположены против часовой стрелки.
            Если результат отрицательный, то точки расположены по часовой стрелке.
            Если результат равен нулю, то точки коллинеарны, то есть лежат на одной линии.
         */
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

    // ArcIntersect - метод, возвращающий true усли два отрезка, заданные точками, пересекаются
    //МНЕ НЕ НРАВИТСЯ ЧТО В ДВУХ МЕТОДА ОДИНАКОВЫЙ КОД
    //НУЖНО ОСТАВИТЬ НИЖНИЙ МЕТОД КАК ЕСТЬ, А В ЕГО ПЕРЕГРУЗКЕ ВЫЗВАТЬ ЕГО ЖЕ
    public static bool ArcIntersect(ToolVector p1, ToolVector p2, ToolVector p3, ToolVector p4)
    {
        //определение направления
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

    //GetCommonPoint - метод, возвращающий общую точку двух ребер
    public static ToolVector GetCommonPoint(Arc a1, Arc a2)
    {
        //тупой перебор
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

    //IsConnectedWith - определяет, связаны ли ребра
    public bool IsConnectedWith(Arc _a)
    {
        // если точка иискомого ребра совпадает с точкой данного ребра
        if (A == _a.A || A == _a.B || B == _a.A || B == _a.B)
            return true;

        else return false;
    }

    //Direction - метод, возвращающий направление через векторное произведение
    private static double Direction(ToolVector pi, ToolVector pj, ToolVector pk)
    {
        return ToolVector.CrossProduct((pk - pi), (pj - pi));
    }

    // OnSegment - метод, который проверяет, лежит ли точка pk на отрезке, образованном двумя другими точками pi и pj
    private static bool OnSegment(ToolVector pi, ToolVector pj, ToolVector pk)
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

        // Создание списка точек для триангуляции
        List<ToolVector> Points = new List<ToolVector>();

        // Добавление "рамочных" точек
        Points.Add(new ToolVector(0, 0));
        Points.Add(new ToolVector(image.Width, 0));
        Points.Add(new ToolVector(image.Width, image.Height));
        Points.Add(new ToolVector(0, image.Height));

        // Добавление случайных точек с минимальным расстоянием в один пиксель
        Random random = new Random();
        int numberOfRandomPoints = 2000; // Установите количество случайных точек
        int minDistance = 2; // Минимальное расстояние между точками в пикселях

        for (int i = 0; i < numberOfRandomPoints; i++)
        {
            ToolVector randomPoint = GenerateRandomPoint(random, image.Width, image.Height, minDistance, Points);
            Points.Add(randomPoint);
        }

        // Создание объекта триангуляции
        Triangulation triangulation = new Triangulation(Points);

        // Рисование и закрашивание треугольников на изображении
        using (Graphics g = Graphics.FromImage(image))
        {
            foreach (var triangle in triangulation.triangles)
            {
                // Определение цвета в вершинах треугольника
                Color color1 = GetPixel(image, (int)triangle.points[0].x, (int)triangle.points[0].y);
                Color color2 = GetPixel(image, (int)triangle.points[1].x, (int)triangle.points[1].y);
                Color color3 = GetPixel(image, (int)triangle.points[2].x, (int)triangle.points[2].y);

                // Вычисление среднего значения цвета
                int avgR = (color1.R + color2.R + color3.R) / 3;
                int avgG = (color1.G + color2.G + color3.G) / 3;
                int avgB = (color1.B + color2.B + color3.B) / 3;

                Color avgColor = Color.FromArgb(avgR, avgG, avgB);

                // Закрашивание треугольника средним значением цвета
                Brush brush = new SolidBrush(avgColor);
                g.FillPolygon(brush, new Point[] {
                    new Point(Math.Max(0, (int)triangle.points[0].x), Math.Max(0, (int)triangle.points[0].y)),
                    new Point(Math.Max(0, (int)triangle.points[1].x), Math.Max(0, (int)triangle.points[1].y)),
                    new Point(Math.Max(0, (int)triangle.points[2].x), Math.Max(0, (int)triangle.points[2].y))
                });
            }
        }

        // Сохранение результата
        image.Save("TriangulatedImage.jpg");
    }

    // Функция для генерации случайной точки с минимальным расстоянием от существующих точек
    static ToolVector GenerateRandomPoint(Random random, int maxWidth, int maxHeight, int minDistance, List<ToolVector> existingPoints)
    {
        while (true)
        {
            int randomX = random.Next(minDistance, maxWidth - minDistance);
            int randomY = random.Next(minDistance, maxHeight - minDistance);

            // Проверка расстояния от новой точки до существующих точек
            bool isValid = true;

            foreach (var existingPoint in existingPoints)
            {
                int distanceSquared = (randomX - (int)existingPoint.x) * (randomX - (int)existingPoint.x) +
                                      (randomY - (int)existingPoint.y) * (randomY - (int)existingPoint.y);

                if (distanceSquared < minDistance * minDistance)
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
                return new ToolVector(randomX, randomY);
        }
    }

    // Функция для получения цвета пикселя с проверкой на границы изображения
    static Color GetPixel(Bitmap image, int x, int y)
    {
        x = Math.Max(0, Math.Min(x, image.Width - 1));
        y = Math.Max(0, Math.Min(y, image.Height - 1));
        return image.GetPixel(x, y);
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