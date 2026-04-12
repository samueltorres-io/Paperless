/* Modules/ToDo/ITaskManager.cs */

namespace Paperless.Modules.ToDo;

public interface ITaskManager
{
   TodoTask CreateTask(string title, string description, int priority);
   List<TodoTask> ListTasks(int? priorityFilter = null);
   TodoTask? CompleteTask(string id);
   bool DeteleTask(string id);
   TodoTask? GetTask(string id);
}