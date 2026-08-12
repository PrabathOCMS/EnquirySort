from enquirysort.models import Action
from enquirysort.openrouter import _parse_json_object


def test_parse_plain_json() -> None:
    data = _parse_json_object(
        '{"action":"respond","confidence":0.8,"reason":"faq","mailing_list":null,"customer_question":"How?"}'
    )
    assert data["action"] == "respond"
    assert data["confidence"] == 0.8


def test_parse_fenced_json() -> None:
    text = """Here you go:
```json
{"action": "route", "confidence": 0.91, "reason": "sales", "mailing_list": "sales", "customer_question": null}
```
"""
    data = _parse_json_object(text)
    assert data["action"] == Action.ROUTE.value
    assert data["mailing_list"] == "sales"


def test_parse_garbage_returns_ignore() -> None:
    data = _parse_json_object("not json at all")
    assert data["action"] == "ignore"
