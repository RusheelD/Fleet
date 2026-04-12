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

    [TestMethod]
    public void Parse_ReturnsPlan_WhenSubflowMarkerIsFollowedByBareJson()
    {
        const string output = """
            G. Sub-Flow Decision
            Large scope; split into 3 parallel child work items.

            SUBFLOW_PLAN_JSON
            {
              "split": true,
              "reason": "Parallel branches are worthwhile.",
              "subflows": [
                {
                  "title": "Engine core",
                  "description": "Implement engine state and rules.",
                  "priority": 1,
                  "difficulty": 5,
                  "tags": ["engine"],
                  "acceptance_criteria": "Engine tests pass.",
                  "subflows": []
                },
                {
                  "title": "AI engine",
                  "description": "Implement evaluation and search.",
                  "priority": 1,
                  "difficulty": 4,
                  "tags": ["ai"],
                  "acceptance_criteria": "AI returns legal moves.",
                  "subflows": []
                },
                {
                  "title": "UI + Hosting + Docs",
                  "description": "Implement UI and docs.",
                  "priority": 2,
                  "difficulty": 4,
                  "tags": ["ui"],
                  "acceptance_criteria": "UI works and docs are updated.",
                  "subflows": []
                }
              ]
            }
            """;

        var parsed = SubFlowPlanner.Parse(output);

        Assert.IsNotNull(parsed);
        Assert.AreEqual(3, parsed.SubFlows.Count);
        Assert.AreEqual("Engine core", parsed.SubFlows[0].Title);
        Assert.AreEqual("AI engine", parsed.SubFlows[1].Title);
        Assert.AreEqual("UI + Hosting + Docs", parsed.SubFlows[2].Title);
    }

    [TestMethod]
    public void Parse_ReturnsNull_WhenPlanDepthExceedsLimit()
    {
        const string output = """
            ```json
            {
              "split": true,
              "reason": "Too deeply nested.",
              "subflows": [
                {
                  "title": "Level 1",
                  "subflows": [
                    {
                      "title": "Level 2",
                      "subflows": [
                        {
                          "title": "Level 3",
                          "subflows": [
                            {
                              "title": "Level 4",
                              "subflows": [
                                {
                                  "title": "Level 5",
                                  "subflows": []
                                }
                              ]
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            ```
            """;

        var parsed = SubFlowPlanner.Parse(output);

        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void Parse_ReturnsNull_WhenAnyNodeExceedsDirectChildLimit()
    {
        const string output = """
            ```json
            {
              "split": true,
              "reason": "Too many direct branches.",
              "subflows": [
                { "title": "Branch 1", "subflows": [] },
                { "title": "Branch 2", "subflows": [] },
                { "title": "Branch 3", "subflows": [] },
                { "title": "Branch 4", "subflows": [] },
                { "title": "Branch 5", "subflows": [] },
                { "title": "Branch 6", "subflows": [] }
              ]
            }
            ```
            """;

        var parsed = SubFlowPlanner.Parse(output);

        Assert.IsNull(parsed);
    }
}
