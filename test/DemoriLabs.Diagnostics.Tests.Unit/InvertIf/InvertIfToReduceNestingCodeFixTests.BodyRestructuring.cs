namespace DemoriLabs.Diagnostics.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingCodeFixTests
{
    [Test]
    public async Task BodyRestructuring_NestedIfFlattened()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void Handle(Request request)
                {
                    {|DL3002:if|} (request != null)
                    {
                        if (request.Payload != null)
                        {
                            request.Payload.Process();
                            Log("Processed");
                        }
                    }
                }

                private static void Log(string s) { }
            }

            public class Request
            {
                public Payload Payload { get; set; }
            }

            public class Payload
            {
                public void Process() { }
            }
            """,
            """
            public class C
            {
                public void Handle(Request request)
                {
                    if (request is null)
                        return;

                    if (request.Payload is null)
                        return;

                    request.Payload.Process();
                    Log("Processed");
                }

                private static void Log(string s) { }
            }

            public class Request
            {
                public Payload Payload { get; set; }
            }

            public class Payload
            {
                public void Process() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_NestedIfWithElseFlattened()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Classify(int score)
                {
                    {|DL3002:if|} (score >= 0)
                    {
                        if (score >= 50)
                        {
                            return "Pass";
                        }
                        else
                        {
                            return "Fail";
                        }
                    }

                    return "Invalid";
                }
            }
            """,
            """
            public class C
            {
                public string Classify(int score)
                {
                    if (score < 0)
                        return "Invalid";

                    if (score < 50)
                        return "Fail";

                    return "Pass";
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_TryCatchPreservedAfterFlattening()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void ImportData(string connectionString)
                {
                    {|DL3002:if|} (!string.IsNullOrEmpty(connectionString))
                    {
                        if (CanConnect(connectionString))
                        {
                            try
                            {
                                var data = FetchData(connectionString);
                                Save(data);
                            }
                            catch (Exception ex)
                            {
                                LogError(ex);
                            }
                        }
                    }
                }

                private static bool CanConnect(string cs) => true;
                private static object FetchData(string cs) => null;
                private static void Save(object data) { }
                private static void LogError(Exception ex) { }
            }
            """,
            """
            using System;

            public class C
            {
                public void ImportData(string connectionString)
                {
                    if (string.IsNullOrEmpty(connectionString))
                        return;

                    if (CanConnect(connectionString) is false)
                        return;

                    try
                    {
                        var data = FetchData(connectionString);
                        Save(data);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                }

                private static bool CanConnect(string cs) => true;
                private static object FetchData(string cs) => null;
                private static void Save(object data) { }
                private static void LogError(Exception ex) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_UsingBlockToUsingDeclaration()
    {
        var test = CreateTest(
            """
            using System.IO;

            public class C
            {
                public void WriteLog(string message)
                {
                    {|DL3002:if|} (message != null)
                    {
                        using (var writer = new StreamWriter("log.txt", true))
                        {
                            writer.WriteLine(message);
                        }
                    }
                }
            }
            """,
            """
            using System.IO;

            public class C
            {
                public void WriteLog(string message)
                {
                    if (message is null)
                        return;

                    using var writer = new StreamWriter("log.txt", true);
                    writer.WriteLine(message);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_LockBlockPreserved()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly object _syncRoot = new();
                private readonly Dictionary<string, object> _cache = new();
                private readonly Dictionary<string, System.DateTime> _timestamps = new();

                public void UpdateCache(string key, object value)
                {
                    {|DL3002:if|} (key != null)
                    {
                        if (value != null)
                        {
                            lock (_syncRoot)
                            {
                                _cache[key] = value;
                                _timestamps[key] = System.DateTime.UtcNow;
                            }
                        }
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly object _syncRoot = new();
                private readonly Dictionary<string, object> _cache = new();
                private readonly Dictionary<string, System.DateTime> _timestamps = new();

                public void UpdateCache(string key, object value)
                {
                    if (key is null)
                        return;

                    if (value is null)
                        return;

                    lock (_syncRoot)
                    {
                        _cache[key] = value;
                        _timestamps[key] = System.DateTime.UtcNow;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_IfElseChainPartiallyFlattened()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void HandleMessage(Message msg)
                {
                    {|DL3002:if|} (msg != null)
                    {
                        if (msg.Type == MessageType.Text)
                        {
                            if (!string.IsNullOrEmpty(msg.Body))
                            {
                                DisplayText(msg.Body);
                            }
                        }
                        else if (msg.Type == MessageType.Image)
                        {
                            if (msg.Attachment != null)
                            {
                                DisplayImage(msg.Attachment);
                            }
                        }
                    }
                }

                private static void DisplayText(string body) { }
                private static void DisplayImage(object attachment) { }
            }

            public class Message
            {
                public MessageType Type { get; set; }
                public string Body { get; set; }
                public object Attachment { get; set; }
            }

            public enum MessageType { Text, Image }
            """,
            """
            public class C
            {
                public void HandleMessage(Message msg)
                {
                    if (msg is null)
                        return;

                    if (msg.Type is MessageType.Text)
                    {
                        if (!string.IsNullOrEmpty(msg.Body))
                        {
                            DisplayText(msg.Body);
                        }
                    }

                    if (msg.Type is not MessageType.Image)
                        return;

                    if (msg.Attachment is null)
                        return;

                    DisplayImage(msg.Attachment);
                }

                private static void DisplayText(string body) { }
                private static void DisplayImage(object attachment) { }
            }

            public class Message
            {
                public MessageType Type { get; set; }
                public string Body { get; set; }
                public object Attachment { get; set; }
            }

            public enum MessageType { Text, Image }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_PartialFlatteningWithRemainingNesting()
    {
        var test = CreateTest(
            """
            public class C
            {
                private bool _disposed;
                private Connection? _connection = null!;

                public void Dispose()
                {
                    {|DL3002:if|} (!_disposed)
                    {
                        if (_connection != null)
                        {
                            if (_connection.State == ConnectionState.Open)
                            {
                                _connection.Close();
                            }
                            _connection.Dispose();
                            _connection = null;
                        }
                        _disposed = true;
                    }
                }
            }

            public enum ConnectionState { Open, Closed }
            public class Connection
            {
                public ConnectionState State { get; set; }
                public void Close() { }
                public void Dispose() { }
            }
            """,
            """
            public class C
            {
                private bool _disposed;
                private Connection? _connection = null!;

                public void Dispose()
                {
                    if (_disposed)
                        return;

                    if (_connection is not null)
                    {
                        if (_connection.State is ConnectionState.Open)
                        {
                            _connection.Close();
                        }
                        _connection.Dispose();
                        _connection = null;
                    }
                    _disposed = true;
                }
            }

            public enum ConnectionState { Open, Closed }
            public class Connection
            {
                public ConnectionState State { get; set; }
                public void Close() { }
                public void Dispose() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_MultipleElseBranchesFlattened()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Evaluate(Submission submission)
                {
                    {|DL3002:if|} (submission != null)
                    {
                        if (submission.IsComplete)
                        {
                            if (submission.Score >= 90)
                            {
                                return "Excellent";
                            }
                            else if (submission.Score >= 70)
                            {
                                return "Good";
                            }
                            else
                            {
                                return "Needs improvement";
                            }
                        }
                        else
                        {
                            return "Incomplete";
                        }
                    }
                    else
                    {
                        return "No submission";
                    }
                }
            }

            public class Submission
            {
                public bool IsComplete { get; set; }
                public int Score { get; set; }
            }
            """,
            """
            public class C
            {
                public string Evaluate(Submission submission)
                {
                    if (submission is null)
                        return "No submission";

                    if (submission.IsComplete is false)
                        return "Incomplete";

                    if (submission.Score >= 90)
                        return "Excellent";

                    if (submission.Score >= 70)
                        return "Good";
                    return "Needs improvement";
                }
            }

            public class Submission
            {
                public bool IsComplete { get; set; }
                public int Score { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BodyRestructuring_NestedElseThrowsFlattened()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void RegisterUser(string username, string password)
                {
                    {|DL3002:if|} (username != null)
                    {
                        if (password != null)
                        {
                            if (username.Length >= 3)
                            {
                                if (password.Length >= 8)
                                {
                                    var user = new User(username, Hash(password));
                                    _store.Save(user);
                                }
                                else
                                {
                                    throw new System.ArgumentException("Password too short");
                                }
                            }
                            else
                            {
                                throw new System.ArgumentException("Username too short");
                            }
                        }
                        else
                        {
                            throw new System.ArgumentNullException(nameof(password));
                        }
                    }
                    else
                    {
                        throw new System.ArgumentNullException(nameof(username));
                    }
                }

                private static string Hash(string s) => s;
                private readonly Store _store = new();
            }

            public class User
            {
                public User(string u, string p) { }
            }

            public class Store
            {
                public void Save(User u) { }
            }
            """,
            """
            public class C
            {
                public void RegisterUser(string username, string password)
                {
                    if (username is null)
                        throw new System.ArgumentNullException(nameof(username));

                    if (password is null)
                        throw new System.ArgumentNullException(nameof(password));

                    if (username.Length < 3)
                        throw new System.ArgumentException("Username too short");

                    if (password.Length < 8)
                        throw new System.ArgumentException("Password too short");

                    var user = new User(username, Hash(password));
                    _store.Save(user);
                }

                private static string Hash(string s) => s;
                private readonly Store _store = new();
            }

            public class User
            {
                public User(string u, string p) { }
            }

            public class Store
            {
                public void Save(User u) { }
            }
            """
        );

        await test.RunAsync();
    }
}
