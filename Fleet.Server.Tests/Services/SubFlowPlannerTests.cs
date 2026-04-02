using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SubFlowPlannerTests
{
    [TestMethod]
    public void Parse_ReturnsNull_WhenSplitIsFalse()
    {
        const string output = """
            Plan text

            SUBFLOW_PLAN_JSON
            ```json
            { "split": false }
            ```
            """;

        var parsed = SubFlowPlanner.Parse(output);

        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void Parse_ReturnsNestedPlan_WhenJsonIsValid()
    {
        const string output = """
            Plan text

            SUBFLOW_PLAN_JSON
            ```json
            {
              "split": true,
              "reason": "Need separate backend and frontend executions.",
              "subflows": [
                {
                  "title": "Backend sync engine",
                  "description": "Build the server-side sync path.",
                  "priority": 2,
                  "difficulty": 4,
                  "tags": ["backend", "sync"],
                  "acceptance_criteria": "Server sync path is implemented.",
                  "subflows": [
                    {
                      "title": "Conflict resolution",
                      "description": "Handle conflict cases.",
                      "priority": 3,
                      "difficulty": 3,
                      "tags": ["backend"],
                      "acceptance_criteria": "Conflict cases are covered.",
                      "subflows": []
                    }
                  ]
                }
              ]
            }
            ```
            """;

        var parsed = SubFlowPlanner.Parse(output);

        Assert.IsNotNull(parsed);
        Assert.AreEqual("Need separate backend and frontend executions.", parsed.Reason);
        Assert.AreEqual(1, parsed.SubFlows.Count);
        Assert.AreEqual("Backend sync engine", parsed.SubFlows[0].Title);
        Assert.AreEqual(1, parsed.SubFlows[0].SubFlows.Count);
        Assert.AreEqual("Conflict resolution", parsed.SubFlows[0].SubFlows[0].Title);
    }
}
