from pathlib import Path

from enquirysort.email_client import load_eml_file


def test_load_sample_eml() -> None:
    path = Path(__file__).resolve().parents[1] / "samples" / "faq_password.eml"
    message = load_eml_file(str(path), uid="local-1")
    assert message.from_address == "customer@example.org"
    assert "password" in message.subject.lower()
    assert "reset" in message.body_text.lower()
