namespace Messaging.Amqp;

public class ProcessingResult
{
    public bool ProcessingFailed { get; }
    public bool UndeliverableHere { get; }

    public ProcessingResult(bool processingFailed, bool undeliverableHere)
    {
        ProcessingFailed = processingFailed;
        UndeliverableHere = undeliverableHere;
    }
}