using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Persistence.Sql.ScriptBuilder;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NUnit.Framework;
using ObjectApproval;

[TestFixture]
public class SubscriptionPersisterTests
{
    string connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=SqlPersistenceTests;Integrated Security=True";
    SubscriptionPersister persister;
    SqlVarient sqlVarient = SqlVarient.MsSqlServer;

    public SubscriptionPersisterTests()
    {
        persister = new SubscriptionPersister(
            connectionBuilder: () =>
            {
                DbConnection connection = new SqlConnection(connectionString);
                connection.Open();
                return Task.FromResult(connection);
            },
            schema: "dbo",
            endpointName: "Endpoint"
            );
    }

    [SetUp]
    public void Setup()
    {
        Execute(SubscriptionScriptBuilder.BuildDropScript(sqlVarient));
        Execute(SubscriptionScriptBuilder.BuildCreateScript(sqlVarient));
    }

    [Test]
    public void Subscribe()
    {
        var type1 = new MessageType("type1", new Version(0, 0, 0, 0));
        var type2 = new MessageType("type2", new Version(0, 0, 0, 0));
        var messageTypes = new List<MessageType>
        {
            type1,
            type2,
        };
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type1, null).Await();
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type2, null).Await();
        persister.Subscribe(new Subscriber("e@machine2", "endpoint"), type1, null).Await();
        persister.Subscribe(new Subscriber("e@machine2", "endpoint"), type2, null).Await();
        var result = persister.GetSubscriberAddressesForMessage(messageTypes, null).Result;
        ObjectApprover.VerifyWithJson(result);
    }

    [Test]
    public void Subscribe_duplicate_add()
    {
        var type1 = new MessageType("type1", new Version(0, 0, 0, 0));
        var type2 = new MessageType("type2", new Version(0, 0, 0, 0));
        var messageTypes = new List<MessageType>
        {
            type1,
            type2,
        };
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type1, null).Await();
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type2, null).Await();
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type1, null).Await();
        persister.Subscribe(new Subscriber("e@machine1", "endpoint"), type2, null).Await();
        var result = persister.GetSubscriberAddressesForMessage(messageTypes, null).Result;
        ObjectApprover.VerifyWithJson(result);
    }

    [Test]
    public void Unsubscribe()
    {
        var message2 = new MessageType("type2", new Version(0, 0));
        var message1 = new MessageType("type1", new Version(0, 0));
        var messageTypes = new List<MessageType>
        {
            message2,
            message1,
        };
        var address1 = new Subscriber("address1@machine1", "endpoint");
        persister.Subscribe(address1, message2, null).Await();
        persister.Subscribe(address1, message1, null).Await();
        var address2 = new Subscriber("address2@machine2", "endpoint");
        persister.Subscribe(address2, message2, null).Await();
        persister.Subscribe(address2, message1, null).Await();
        persister.Unsubscribe(address1, message2, null).Await();
        var result = persister.GetSubscriberAddressesForMessage(messageTypes, null).Result;
        ObjectApprover.VerifyWithJson(result);
    }

    void Execute(string script)
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = script;
                command.AddParameter("schema", "dbo");
                command.AddParameter("endpointName", "Endpoint");
                command.ExecuteNonQuery();
            }
        }
    }

}