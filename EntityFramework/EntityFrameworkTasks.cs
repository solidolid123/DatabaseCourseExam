using DatabaseCourseExam.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatabaseCourseExam.EntityFramework;

public static class EntityFrameworkTasks
{
    // TODO (задание 11): Вывести студентов с названием группы; обеспечить загрузку связанной сущности без обращения к незагруженному свойству.
    public static async Task ShowStudentsAsync()
    {
        await using var context = SchoolDbContextFactory.Create();

        List<Student> students = await context.Students
            .Include(student => student.Group)
            .OrderBy(student => student.Id)
            .ToListAsync();

        foreach (Student student in students)
        {
            Console.WriteLine(
                $"{student.Id}. {student.FirstName} {student.LastName}, " +
                $"возраст: {student.Age}, группа: {student.Group.Name}");
        }
    }

    // TODO (задание 2): Добавить обработку DbUpdateException и объяснить, почему ограничение уникальности должно обеспечиваться БД.
    // TODO (задание 7): Проверить входные данные и отсутствие студента; сохранить изменения через SaveChangesAsync.
    public static async Task UpdateStudentAgeAsync(
        int studentId,
        int newAge)
    {
        if (newAge < 14 || newAge > 100)
        {
            Console.WriteLine("Возраст должен быть в диапазоне от 14 до 100 лет.");
            return;
        }

        await using var context = SchoolDbContextFactory.Create();

        Student? student = await context.Students
            .FirstOrDefaultAsync(x => x.Id == studentId);

        if (student is null)
        {
            Console.WriteLine("Студент не найден.");
            return;
        }

        student.Age = newAge;

        try
        {
            int affectedRows = await context.SaveChangesAsync();
            Console.WriteLine($"Возраст изменён. Сохранено изменений: {affectedRows}");
        }
        catch (DbUpdateException)
        {
            Console.WriteLine("Ошибка обновления базы данных.");
            // Ограничение уникальности должно быть в БД для гарантии целостности данных в хранилище.
        }
    }

    // TODO (задание 6): Выбирать только нужные поля через Select и добавить вычисляемый признак «мест нет».
    public static async Task ShowCoursesAsync()
    {
        await using var context = SchoolDbContextFactory.Create();

        var courses = await context.Courses
            .OrderBy(course => course.Name)
            .Select(course => new
            {
                course.Name,
                course.Price,
                course.AvailablePlaces,
                NoPlaces = course.AvailablePlaces == 0
            })
            .ToListAsync();

        foreach (var course in courses)
        {
            Console.WriteLine(
                $"{course.Name} | Цена: {course.Price:N2} | " +
                $"Свободных мест: {course.AvailablePlaces} | Мест нет: {(course.NoPlaces ? "Да" : "Нет")}");
        }
    }
}