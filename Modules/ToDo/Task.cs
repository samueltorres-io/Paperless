/* Modules/ToDo/Task.cs */

public class Task
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public bool IsComplete { get; private set; }
    public int Priority { get; private set; }

    public Task(string title, string description, int priority)
    {
        Title = title;
        Description = description;
        IsComplete = false;
        Priority = priority;
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty!");

        Title = title;
    }

    public void CompleteTask()
    {
        IsComplete = true;
    }

}