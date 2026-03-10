class Program
{
    static void Main()
    {
        List<Student> students = new List<Student>()
        {
            new Student{ Id = 1, Name = "Alice", Age = 20 },
            new Student{ Id = 2, Name = "Bob", Age = 22 },
            new Student{ Id = 3, Name = "Charlie", Age = 21 },
            new Student{ Id = 4, Name = "David", Age = 22 },
        };

        List<Subjects> subjects = new List<Subjects>()
        {
            new Subjects{ Id = 1, Subject = "Math" },
            new Subjects{ Id = 2, Subject = "Science" },
            new Subjects{ Id = 3, Subject = "History" }
        };

        var result = students.Where(s => s.Age > 20).Select(s => new { s.Id, s.Name, s.Age });
        foreach (var student in result)
        {
            Console.WriteLine($"Id: {student.Id}, Name: {student.Name}, Age: {student.Age}");
        }

        var studentAge = students.GroupBy(s => s.Age);
        Console.WriteLine("\nStudents grouped by age:");
        foreach (var group in studentAge)
        {
            Console.WriteLine($"Age: {group.Key}");
            foreach (var student in group)
            {
                Console.WriteLine($"  Id: {student.Id}, Name: {student.Name}");
            }
        }

        var studentSubject = students.Join(subjects, s => s.Id, sub => sub.Id, (s, sub) => new { s.Name, sub.Subject });
        Console.WriteLine("\nStudents with their subjects:");
        foreach (var name in students)
        {
            Console.WriteLine($"Name: {name.Name}");
            foreach (var item in studentSubject)
            {
                Console.WriteLine($"Subject: {item.Subject}");
            }
        }

        var studentName = students.Any(s => s.Name == "Alice");
        Console.WriteLine($"\nIs there any student named Alice? {studentName}");

        var studentAll = students.All(s => s.Age > 20);
        Console.WriteLine($"\nAre all students older than 20? {studentAll}");

        var studentTwo = students.Take(2);
        Console.WriteLine("\nFirst two students:");
        foreach (var item in studentTwo)
        {
            Console.WriteLine($"\nId: {item.Id}, Name: {item.Name}, Age: {item.Age}");
        }

        var studentSkip = students.Skip(1);
        Console.WriteLine("\nStudents after skipping the first one:");
        foreach (var item in studentSkip)
        {
            Console.WriteLine($"\nId: {item.Id}, Name: {item.Name}, Age: {item.Age}");
        }

        var studentSubjectUnion = students.Select(s=> s.Name).Union(subjects.Select(sub => sub.Subject));
        Console.WriteLine("\nUnion of student names and subjects:");
        foreach (var item in studentSubjectUnion)
        {
            Console.WriteLine(item);
        }

        var studentSubjectIntersect = students.Select(s => s.Name).Intersect(subjects.Select(sub => sub.Subject));
        Console.WriteLine("\nIntersection of student names and subjects:");
        if (!studentSubjectIntersect.Any())
        {
            Console.WriteLine("No common elements found.");
        }
        else
        {
            foreach (var item in studentSubjectIntersect)
            {
                Console.WriteLine(item);
            }
        }

        var distinctStudent = students.Select(s => s.Age).Distinct();
        Console.WriteLine("\nDistinct ages of students:");
        if (!distinctStudent.Any())
        {
            Console.WriteLine("No distinct ages found.");
        }
        else
        {
            foreach (var item in distinctStudent)
            {
                Console.WriteLine(item);
            }
        }

        var exceptionalStudent = students.Except(subjects.Select(sub => new Student { Name = sub.Subject }));
        Console.WriteLine("\nStudents except those with subjects as names:");
        if (!exceptionalStudent.Any())
        {
            Console.WriteLine("No students found after except operation.");
        }
        else
        {
            foreach (var item in exceptionalStudent)
            {
                Console.WriteLine($"Id: {item.Id}, Name: {item.Name}, Age: {item.Age}");
            }
        }

    }
    class Student
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }

    }

    class Subjects
    {
        public int Id { get; set; }
        public string Subject { get; set; }
    }
}