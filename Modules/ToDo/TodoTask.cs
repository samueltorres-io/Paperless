using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paperless.Modules.ToDo;

public class TodoTask
{
    public string Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public bool IsComplete { get; private set; }
    public int Priority { get; private set; }

    /* Constructor */
    [JsonConstructor]
    public TodoTask(string id, string title, string description, bool isComplete, int priority)
    {
        Id = id;
        Title = title;
        Description = description;
        IsComplete = isComplete;
        Priority = priority;
    }

    public TodoTask(string title, string description, int priority)
    {
        Id = Guid.NewGuid().ToString("N")[..16];
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

    public void UpdatePriority(int priority)
    {

        if (priority < 1 || priority > 5)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be 1-5!");

        Priority = priority;
    }

    public override string ToString()
    {
        var status = IsComplete ? "✓" : "○";
        var priorityLabel = Priority switch
        {
            1 => "low",
            2 => "medium",
            3 => "high",
            4 => "urgent",
            5 => "critical",
            _ => "unknown"
        };

        return $" [{status}] {Id} {Title} (priority: {priorityLabel})";
    }
}