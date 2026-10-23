using System.Data;
using DatabaseCourseExam.Data;
using Npgsql;

namespace DatabaseCourseExam.AdoNet;

public static class DisconnectedTasks
{
    // TODO (задание 5): Реализовать добавление новой строки в DataTable с проверкой возраста и правильным RowState Added.
    public static void AddStudent(DataTable table, string firstName, string lastName, int age, int groupId)
    {
        if (age < 14 || age > 100)
        {
            Console.WriteLine("Возраст должен быть от 14 до 100.");
            return;
        }

        DataRow newRow = table.NewRow();
        newRow["first_name"] = firstName;
        newRow["last_name"] = lastName;
        newRow["age"] = age;
        newRow["group_id"] = groupId;

        table.Rows.Add(newRow);
        Console.WriteLine($"Студент добавлен. RowState: {newRow.RowState}");
    }

    public static async Task<DataTable> LoadStudentsAsync()
    {
        var table = new DataTable("Students");

        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        const string sql = """
            SELECT id, first_name, last_name, age, group_id
            FROM students
            ORDER BY id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        using var adapter = new NpgsqlDataAdapter(command);

        adapter.Fill(table);
        table.PrimaryKey = [table.Columns["id"]!];

        return table;
    }

    public static void PrintTable(DataTable table)
    {
        foreach (DataRow row in table.Rows)
        {
            Console.WriteLine(
                $"{row["id"]}: {row["first_name"]} " +
                $"{row["last_name"]}, возраст {row["age"]}");
        }
    }

    // TODO (задание 13): Вывести RowState до и после изменения; объяснить разницу Unchanged и Modified.
    public static void ChangeAgeInMemory(
        DataTable table,
        int studentId,
        int newAge)
    {
        DataRow? row = table.Rows.Find(studentId);

        if (row is null)
        {
            Console.WriteLine("Студент не найден в DataTable.");
            return;
        }

        Console.WriteLine($"RowState до изменения: {row.RowState}");
        row["age"] = newAge;
        Console.WriteLine($"RowState после изменения: {row.RowState}");
        // Unchanged: строка не изменялась с момента последней загрузки или вызова AcceptChanges.
        // Modified: значение одного или нескольких столбцов было изменено, но изменения ещё не сохранены в БД (не вызван AcceptChanges).
    }

    // TODO (задание 8): После Update вывести количество сохранённых строк и проверить, остались ли строки в состоянии Modified.
    public static int SaveChanges(DataTable table)
    {
        using var connection = DatabaseConnection.Create();
        connection.Open();

        const string sql = """
        UPDATE students
        SET age = @age
        WHERE id = @id;
        """;

        using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.Add("@age", NpgsqlTypes.NpgsqlDbType.Integer,
            4, "age");

        command.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Integer,
            4, "id");

        using var adapter = new NpgsqlDataAdapter();
        adapter.UpdateCommand = command;

        int affectedRows = adapter.Update(table);
        Console.WriteLine($"Сохранено изменений: {affectedRows}");

        int modifiedCount = 0;
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Modified)
            {
                modifiedCount++;
            }
        }
        Console.WriteLine($"Строк в состоянии Modified после сохранения: {modifiedCount}");

        return affectedRows;
    }
}