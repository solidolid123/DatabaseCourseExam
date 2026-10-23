using DatabaseCourseExam.Data;
using Npgsql;
using NpgsqlTypes;

namespace DatabaseCourseExam.AdoNet;

public static class AdoNetTasks
{
    // TODO (задание 9): Проверить потенциально NULL-значение через IsDBNull прежде чем вызывать GetString/GetInt32.
    // TODO (задание 12): Добавить фильтр по group_id: считать ID из ввода, передать его параметром NpgsqlParameter и вывести только выбранную группу.
    public static async Task ShowStudentsAsync(int? groupId = null)
    {
        if (!groupId.HasValue)
        {
            Console.Write("Введите ID группы (или оставьте пустым для всех): ");
            string? input = Console.ReadLine();
            if (int.TryParse(input, out int gId))
            {
                groupId = gId;
            }
        }

        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        string sql = """
            SELECT s.id, s.first_name, s.last_name, s.age, g.name
            FROM students AS s
            JOIN student_groups AS g ON g.id = s.group_id
            """;

        if (groupId.HasValue)
        {
            sql += " WHERE s.group_id = @GroupId";
        }

        sql += " ORDER BY s.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        if (groupId.HasValue)
        {
            command.Parameters.Add("@GroupId", NpgsqlDbType.Integer).Value = groupId.Value;
        }

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            string firstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            string lastName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            int age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            string groupName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

            Console.WriteLine(
                $"{id}. " +
                $"{firstName} {lastName}, " +
                $"возраст: {age}, " +
                $"группа: {groupName}");
        }
    }

    // TODO (задание 1): Проверить диапазон возраста 14–100 до обращения к БД; сообщить число изменённых строк и обработать отсутствие студента.
    public static async Task UpdateStudentAgeAsync(
        int studentId,
        int newAge)
    {
        if (newAge < 14 || newAge > 100)
        {
            Console.WriteLine("Возраст должен быть в диапазоне от 14 до 100 лет.");
            return;
        }

        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        const string sql = """
            UPDATE students
            SET age = @Age
            WHERE id = @StudentId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.Add("@Age", NpgsqlDbType.Integer).Value = newAge;
        command.Parameters.Add("@StudentId", NpgsqlDbType.Integer).Value =
            studentId;

        int affectedRows = await command.ExecuteNonQueryAsync();

        Console.WriteLine(
            affectedRows == 0
                ? "Студент не найден."
                : $"Изменено записей: {affectedRows}");
    }

    // TODO (задание 10): До изменения доступных мест проверить существование студента и отсутствие повторной записи; при любой ошибке откатить транзакцию.
    public static async Task EnrollStudentAsync(
        int studentId,
        int courseId)
    {
        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string checkStudentSql = "SELECT 1 FROM students WHERE id = @StudentId;";
            await using var checkStudentCmd = new NpgsqlCommand(checkStudentSql, connection, transaction);
            checkStudentCmd.Parameters.Add("@StudentId", NpgsqlDbType.Integer).Value = studentId;
            var studentExists = await checkStudentCmd.ExecuteScalarAsync();

            if (studentExists is null)
            {
                throw new InvalidOperationException("Студент не найден.");
            }

            const string checkEnrollmentSql = "SELECT 1 FROM student_courses WHERE student_id = @StudentId AND course_id = @CourseId;";
            await using var checkEnrollmentCmd = new NpgsqlCommand(checkEnrollmentSql, connection, transaction);
            checkEnrollmentCmd.Parameters.Add("@StudentId", NpgsqlDbType.Integer).Value = studentId;
            checkEnrollmentCmd.Parameters.Add("@CourseId", NpgsqlDbType.Integer).Value = courseId;
            var alreadyEnrolled = await checkEnrollmentCmd.ExecuteScalarAsync();

            if (alreadyEnrolled is not null)
            {
                throw new InvalidOperationException("Студент уже записан на этот курс.");
            }

            const string updateSql = """
                UPDATE courses
                SET available_places = available_places - 1
                WHERE id = @CourseId
                  AND available_places > 0
                RETURNING available_places;
                """;

            await using var updateCommand =
                new NpgsqlCommand(updateSql, connection, transaction);

            updateCommand.Parameters.Add(
                "@CourseId", NpgsqlDbType.Integer).Value = courseId;

            object? result = await updateCommand.ExecuteScalarAsync();

            if (result is null)
            {
                throw new InvalidOperationException(
                    "Курс не найден или свободных мест нет.");
            }

            const string insertSql = """
                INSERT INTO student_courses (student_id, course_id)
                VALUES (@StudentId, @CourseId);
                """;

            await using var insertCommand =
                new NpgsqlCommand(insertSql, connection, transaction);

            insertCommand.Parameters.Add(
                "@StudentId", NpgsqlDbType.Integer).Value = studentId;

            insertCommand.Parameters.Add(
                "@CourseId", NpgsqlDbType.Integer).Value = courseId;

            await insertCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            Console.WriteLine("Студент записан на курс.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Операция отменена: {ex.Message}");
        }
    }
}