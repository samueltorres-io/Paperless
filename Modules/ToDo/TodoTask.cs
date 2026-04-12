/* Modules/ToDo/Task.cs */

public class TodoTask
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public bool IsComplete { get; private set; }
    public int Priority { get; private set; }

    /* Constructor */
    public TodoTask(string title, string description, int priority)
    {
        Title = title;
        Description = description;
        IsComplete = false;
        Priority = priority;
    }

    /* Methods */
    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty!");

        Title = title;
    }

    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty!");

        Description = description;
    }

    public void CompleteTask()
    {
        IsComplete = true;
    }

    public void UpdatePriority(ushort priority)
    { /* ushort -> 0:65535 2bytes */
        Priority = priority;
    }

}