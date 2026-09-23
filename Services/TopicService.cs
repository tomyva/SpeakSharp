using SpeakSharp.Models;

namespace SpeakSharp.Services;

public sealed class TopicService
{
    private static readonly PracticeTopic[] Topics =
    [
        new("Everyday", "Describe a small habit that has improved your life."),
        new("Persuasion", "Convince a friend to try a hobby you enjoy."),
        new("Storytelling", "Tell the story of a mistake that taught you something useful."),
        new("Work", "Explain a complex part of your work to a curious twelve-year-old."),
        new("Leadership", "What makes someone trustworthy when leading a team?"),
        new("Imagination", "If your city had one car-free day each week, what would change?"),
        new("Reflection", "What advice would you give yourself from five years ago?"),
        new("Quick thinking", "Which everyday object is underrated, and why?"),
        new("Opinion", "Should meetings default to twenty-five minutes? Make your case."),
        new("Future", "Describe one skill you want to master and how you will learn it."),
    ];

    private int _lastIndex = -1;

    public PracticeTopic Next()
    {
        int index;
        do index = Random.Shared.Next(Topics.Length); while (Topics.Length > 1 && index == _lastIndex);
        _lastIndex = index;
        return Topics[index];
    }
}
