using System;
using System.Globalization;
using System.Text;
using System.Data.SQLite;

namespace Lab_SQLite
{
    class Program
    {
        const string ConnStr = "Data Source=lab.db;Version=3;";

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            InitDb();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("===== ГОЛОВНЕ МЕНЮ =====");
                Console.WriteLine("1 - Додати групу (INSERT)");
                Console.WriteLine("2 - Додати студента (INSERT)");
                Console.WriteLine("3 - Змінити назву групи для факультету (UPDATE)");
                Console.WriteLine("4 - Видалити всі групи факультету (DELETE)");
                Console.WriteLine("5 - Показати обидві таблиці (SELECT)");
                Console.WriteLine("0 - Вихід");
                Console.Write("Ваш вибір: ");

                switch (Console.ReadLine())
                {
                    case "1": AddGroup(); break;
                    case "2": AddStudent(); break;
                    case "3": UpdateGroupName(); break;
                    case "4": DeleteFacultyGroups(); break;
                    case "5": ShowTables(); break;
                    case "0": return;
                    default: Console.WriteLine("Невірний пункт меню."); break;
                }
            }
        }

        // Відкриття підключення з увімкненими PRAGMA (діють лише в межах з'єднання)
        static SQLiteConnection OpenConn()
        {
            var conn = new SQLiteConnection(ConnStr);
            conn.Open();
            using (var cmd = new SQLiteCommand(
                "PRAGMA foreign_keys = ON; PRAGMA case_sensitive_like = ON;", conn))
                cmd.ExecuteNonQuery();
            return conn;
        }

        // Створення таблиць і тригерів, якщо їх ще немає
        static void InitDb()
        {
            string sql = @"
CREATE TABLE IF NOT EXISTS Groups (
    name    TEXT PRIMARY KEY NOT NULL,
    faculty TEXT NOT NULL,
    headman TEXT
);
CREATE TABLE IF NOT EXISTS Students (
    student_id TEXT PRIMARY KEY NOT NULL,
    full_name  TEXT NOT NULL,
    birth_date TEXT,
    avg_score  REAL,
    group_name TEXT,
    FOREIGN KEY (group_name) REFERENCES Groups(name) DEFERRABLE INITIALLY DEFERRED
);
CREATE TRIGGER IF NOT EXISTS trg_groups_update
BEFORE UPDATE OF name ON Groups
BEGIN
    UPDATE Students SET group_name = NEW.name WHERE group_name = OLD.name;
END;
CREATE TRIGGER IF NOT EXISTS trg_groups_delete
BEFORE DELETE ON Groups
BEGIN
    UPDATE Students SET group_name = NULL WHERE group_name = OLD.name;
END;";
            using (var conn = OpenConn())
            using (var cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        static string Ask(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine().Trim();
        }

        // INSERT у головну таблицю
        static void AddGroup()
        {
            string name = Ask("Назва групи: ");
            string faculty = Ask("Факультет: ");
            string headman = Ask("Староста: ");
            try
            {
                using (var conn = OpenConn())
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO Groups(name, faculty, headman) VALUES(@n,@f,@h)", conn))
                {
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@f", faculty);
                    cmd.Parameters.AddWithValue("@h", headman);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("Групу додано.");
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("Помилка: " + ex.Message);
            }
        }

        // INSERT у підлеглу таблицю з перехопленням порушення FK
        static void AddStudent()
        {
            string id = Ask("№ студентського квитка: ");
            string fio = Ask("П.І.П.: ");
            string birth = Ask("Дата народження (РРРР-ММ-ДД): ");
            string avgStr = Ask("Середній бал: ");
            string group = Ask("Група: ");

            double avg;
            if (!double.TryParse(avgStr.Replace(',', '.'), NumberStyles.Any,
                                 CultureInfo.InvariantCulture, out avg))
            {
                Console.WriteLine("Середній бал введено некоректно.");
                return;
            }

            try
            {
                using (var conn = OpenConn())
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO Students(student_id, full_name, birth_date, avg_score, group_name) " +
                    "VALUES(@id,@fn,@bd,@avg,@g)", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@fn", fio);
                    cmd.Parameters.AddWithValue("@bd", birth);
                    cmd.Parameters.AddWithValue("@avg", avg);
                    cmd.Parameters.AddWithValue("@g", group);
                    cmd.ExecuteNonQuery();
                }
                Console.WriteLine("Студента додано.");
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("Помилка вставки (можливо, такої групи не існує " +
                                  "або порушено унікальність): " + ex.Message);
            }
        }

        // UPDATE: змінити назву групи для одного факультету
        static void UpdateGroupName()
        {
            string faculty = Ask("Факультет: ");
            string oldName = Ask("Стара назва групи: ");
            string newName = Ask("Нова назва групи: ");
            try
            {
                using (var conn = OpenConn())
                using (var cmd = new SQLiteCommand(
                    "UPDATE Groups SET name=@new WHERE name=@old AND faculty=@f", conn))
                {
                    cmd.Parameters.AddWithValue("@new", newName);
                    cmd.Parameters.AddWithValue("@old", oldName);
                    cmd.Parameters.AddWithValue("@f", faculty);
                    int n = cmd.ExecuteNonQuery();
                    Console.WriteLine("Змінено рядків: " + n);
                }
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("Помилка: " + ex.Message);
            }
        }

        // DELETE: видалити всі групи одного факультету
        static void DeleteFacultyGroups()
        {
            string faculty = Ask("Факультет, групи якого видалити: ");
            try
            {
                using (var conn = OpenConn())
                using (var cmd = new SQLiteCommand(
                    "DELETE FROM Groups WHERE faculty=@f", conn))
                {
                    cmd.Parameters.AddWithValue("@f", faculty);
                    int n = cmd.ExecuteNonQuery();
                    Console.WriteLine("Видалено груп: " + n);
                }
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("Помилка: " + ex.Message);
            }
        }

        // SELECT: перевірка роботи тригерів
        static void ShowTables()
        {
            using (var conn = OpenConn())
            {
                Console.WriteLine("\n--- Groups ---");
                Console.WriteLine("{0,-10} {1,-12} {2,-15}", "name", "faculty", "headman");
                using (var cmd = new SQLiteCommand("SELECT * FROM Groups", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        Console.WriteLine("{0,-10} {1,-12} {2,-15}", r["name"], r["faculty"], r["headman"]);

                Console.WriteLine("\n--- Students ---");
                Console.WriteLine("{0,-8} {1,-20} {2,-12} {3,-6} {4,-10}",
                    "id", "full_name", "birth", "avg", "group");
                using (var cmd = new SQLiteCommand("SELECT * FROM Students", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string g = r.IsDBNull(4) ? "NULL" : r.GetString(4);
                        Console.WriteLine("{0,-8} {1,-20} {2,-12} {3,-6} {4,-10}",
                            r["student_id"], r["full_name"], r["birth_date"], r["avg_score"], g);
                    }
            }
        }
    }
}