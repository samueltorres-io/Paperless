/* Modules/ToDo/TodoManager.cs */

namespace Paperless.Modules.ToDo;

public class TodoManager : ITaskManager
{

    private readonly TodoRepository _repository;

    public TodoManager(TodoRepository repository)
    {
        _repository = repository;
    }

    /* Cria e persiste uma nova tarefa */
    public TodoTask CreateTask(string title, string description, int priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required!");

        //todo: Add validaçaõ de description por tamanho de texto e as validações de validade

        var item = new TodoTask(title, description, priority);

        var tasks = _repository.LoadAll();
        tasks.Add(item);
        _repository.SaveAll(tasks);

        return item;
    }

}