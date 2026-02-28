using System;
using System.Reflection.Metadata.Ecma335;

namespace StudentManagement
{
    class Program
    {
        public static void Main(string[] args)
        { 
            StudentService service = new StudentService();
            bool exit = false;

            Console.WriteLine("\n---You are now using Student Management System---");
            Console.WriteLine("============================================");
            Console.WriteLine("Please select service to continue:");
            Console.WriteLine("1. Add Student");
            Console.WriteLine("2. View Students");
            Console.WriteLine("3. Search Students");
            Console.WriteLine("4. Update Student");
            Console.WriteLine("5. Delete Student");
            Console.WriteLine("6. Exit");

            switch(Console.ReadLine())
                {
                case "1":
                    service.AddStudent();
                    break;
                case "2":
                    service.ViewStudents();
                    break;
                case "3":
                    service.SearchStudents();
                    break;
                case "4":
                    service.UpdateStudents();
                    break;
                case "5":
                    service.DeleteStudents();
                    break;
                case "6":
                    exit = true;
                    break;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;




            }
        }
    }
}