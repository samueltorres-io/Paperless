/* Modules/ToDo/TodoManager.cs */

namespace Paperless.Modules.ToDo;

public class TodoManager : ITaskManager
{

    private readonly TodoRepository _repository;

    public TodoManager(TodoRepository repository)
    {
        _repository = repository;
    }

    

}