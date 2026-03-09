class Program
{
    static void Main()
    {
        List<Student> students = new List<Student>()
        {
            new Student{ Id = 1, Name = "Alice", Age = 20 },
            new Student{ Id = 2, Name = "Bob", Age = 22 },
            new Student{ Id = 3, Name = "Charlie", Age = 21 }
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
    }
    class Student
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }

    }
}