from __future__ import annotations

from typing import Literal

from langgraph.graph import END, START, StateGraph

from enquirysort_agent.nodes import apply_rules_and_draft_node, classify_node, retrieve_node
from enquirysort_agent.state import TriageState


def _route_after_classify(state: TriageState) -> Literal["retrieve", "end"]:
    if state.get("action") == "respond":
        return "retrieve"
    return "end"


def build_triage_graph():
    """
    LangGraph workflow:
      classify → (respond) retrieve → apply_rules_and_draft → END
               → (route|ignore) END
    """
    graph = StateGraph(TriageState)
    graph.add_node("classify", classify_node)
    graph.add_node("retrieve", retrieve_node)
    graph.add_node("apply_rules_and_draft", apply_rules_and_draft_node)

    graph.add_edge(START, "classify")
    graph.add_conditional_edges(
        "classify",
        _route_after_classify,
        {"retrieve": "retrieve", "end": END},
    )
    graph.add_edge("retrieve", "apply_rules_and_draft")
    graph.add_edge("apply_rules_and_draft", END)
    return graph.compile()


triage_graph = build_triage_graph()
