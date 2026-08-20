from __future__ import annotations

from langchain_aws import ChatBedrockConverse

from enquirysort_agent.config import settings


def build_chat_model(*, temperature: float | None = None) -> ChatBedrockConverse:
    """Amazon Bedrock chat model via LangChain."""
    return ChatBedrockConverse(
        model=settings.bedrock_model_id,
        region_name=settings.aws_region,
        temperature=settings.temperature if temperature is None else temperature,
    )
