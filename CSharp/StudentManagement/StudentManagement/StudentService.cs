using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StudentManagement
{
    public class StudentService
    {
        private List<Student> students;
        private const string filePath = "students.json";
        public StudentService()
        {
            students = LoadFromFile();
        }
        public void AddStudent()
        {
            Console.WriteLine("Enter Student ID:");
            int id = int.Parse(Console.ReadLine());
            if(students.Any(s=>s.studentId == id))
            {
                Console.WriteLine("Student with this ID already exists");
                return;
            }
            Console.WriteLine("Enter Student's Name:");
            string name = Console.ReadLine();

            Console.WriteLine("Enter Degree:");
            string degree = Console.ReadLine();

            Console.WriteLine("Enter Course:");
            string course = Console.ReadLine();

            Console.WriteLine("Enter the start year:");
            int year = int.Parse(Console.ReadLine());

            students.Add(new Student
            {
                studentId = id,
                studentName = name,
                studentDegree = degree,
                studentCourse = course,
                studentYear = year
            });

            SaveToFile();
            Console.WriteLine("Student details are added successfully.");

        }
        public void ViewStudents()
        {
            if(!students.Any())
            {
                Console.WriteLine("No students found");
                return;
            }
            foreach(var student in students)
            {
                Console.WriteLine($"Student ID: {student.studentId},Student Name: {student.studentName}, Student Degree: {student.studentDegree}, Student Course = {student.studentCourse}, Student Year : {student.studentYear}");
            }

        }

        public void SearchStudents()
        {
            Console.WriteLine("Enter student ID to search: ");
            int id = int.Parse(Console.ReadLine());

            var student = students.FirstOrDefault(s => s.studentId == id);
            if(student == null)
            {
                Console.WriteLine($"Student with ID {id} not found.");
                return;
            }
            Console.WriteLine($"Student ID: {student.studentId},Student Name: {student.studentName}, Student Degree: {student.studentDegree}, Student Course = {student.studentCourse}, Student Year : {student.studentYear}");

        }

        public void UpdateStudents()
        {
            Console.WriteLine("Enter student ID to update its details: ");
            int id = int.Parse(Console.ReadLine());

            var student = students.FirstOrDefault(s => s.studentId == id);
            if (student == null)
            {
                Console.WriteLine($"Student with ID {id} not found.");
                return;
            }

            Console.Write("Enter New Name: ");
            student.studentName = Console.ReadLine();

            Console.Write("Enter New Degree: ");
            student.studentDegree = Console.ReadLine();

            Console.Write("Enter New Course: ");
            student.studentCourse = Console.ReadLine();

            Console.WriteLine("Enter new starting year: ");
            student.studentYear = int.Parse(Console.ReadLine());

            SaveToFile();
            Console.WriteLine("Student updated successfully.");
        }

        public void DeleteStudents()
        {
            Console.Write("Enter Student ID to delete: ");
            int id = int.Parse(Console.ReadLine());

            var student = students.FirstOrDefault(s => s.studentId == id);
            if (student == null)
            {
                Console.WriteLine("Student not found.");
                return;
            }

            students.Remove(student);
            SaveToFile();
            Console.WriteLine("Student details deleted successfully.");
        }

        private void SaveToFile()
        {
            string json = JsonSerializer.Serialize(students, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        private List<Student> LoadFromFile()
        {
            if (!File.Exists(filePath))
                return new List<Student>();
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<Student>>(json) ?? new List<Student>();
        }
    }
}