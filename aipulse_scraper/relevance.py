"""Score how AI-related an article is, 0 to 100.

Simple keyword weighting: strong terms (LLM, transformer) count more than
weak ones (model, data). Title matches count double. Anything scoring below
MIN_SCORE is dropped by the pipeline, except for sources that are AI-only
by construction (arXiv cs.AI, the company blogs), which get a floor.
"""

from __future__ import annotations

import re

STRONG = {
    "artificial intelligence": 30, "machine learning": 25, "deep learning": 25,
    "large language model": 30, "llm": 28, "llms": 28, "gpt": 22, "chatgpt": 25,
    "openai": 25, "anthropic": 25, "claude": 20, "gemini": 18, "deepmind": 25,
    "transformer": 20, "neural network": 25, "neural net": 20, "diffusion model": 25,
    "stable diffusion": 25, "midjourney": 22, "generative ai": 30, "genai": 25,
    "foundation model": 28, "reinforcement learning": 25, "agi": 22,
    "ai agent": 28, "ai agents": 28, "agentic": 22, "rag": 12, "fine-tuning": 20,
    "fine tuning": 20, "rlhf": 25, "mistral": 18, "llama": 18, "hugging face": 22,
    "huggingface": 22, "nvidia": 12, "gpu": 8, "tpu": 12, "inference": 10,
    "multimodal": 18, "text-to-image": 22, "text-to-video": 22, "speech recognition": 18,
    "computer vision": 22, "nlp": 18, "natural language processing": 22,
    "copilot": 15, "alignment": 10, "ai safety": 28, "superintelligence": 25,
    "mythos": 15, "fable": 8, "sora": 12, "veo": 10, "o1": 6, "o3": 6,
}

WEAK = {
    "ai": 10, "model": 4, "models": 4, "training": 4, "dataset": 6, "benchmark": 6,
    "embedding": 8, "embeddings": 8, "prompt": 6, "prompting": 8, "chatbot": 10,
    "robot": 6, "robotics": 8, "autonomous": 6, "automation": 3, "token": 3,
}

MIN_SCORE = 15

_WORD = re.compile(r"[a-z0-9][a-z0-9\-\.]*")


def _tokens(text: str) -> list[str]:
    return _WORD.findall(text.lower())


def score(title: str, body: str = "", *, floor: float = 0.0) -> float:
    title_l = title.lower()
    body_l = body.lower()
    total = 0.0

    for phrase, weight in STRONG.items():
        if phrase in title_l:
            total += weight * 2
        elif phrase in body_l:
            total += weight

    title_tokens = set(_tokens(title_l))
    body_tokens = set(_tokens(body_l))
    for word, weight in WEAK.items():
        if word in title_tokens:
            total += weight * 2
        elif word in body_tokens:
            total += weight

    return float(max(floor, min(100.0, total)))


def tags_for(title: str, body: str = "") -> list[str]:
    """A few short tags for the UI chips."""
    text = f"{title} {body}".lower()
    tags: list[str] = []
    rules = [
        ("llm", ("llm", "large language model", "gpt", "claude", "gemini", "llama", "mistral")),
        ("research", ("arxiv", "paper", "benchmark", "we propose", "we present")),
        ("agents", ("agent", "agentic")),
        ("vision", ("computer vision", "image", "diffusion", "text-to-image", "video")),
        ("safety", ("safety", "alignment", "regulation", "policy", "ethics")),
        ("hardware", ("gpu", "nvidia", "tpu", "chip", "semiconductor")),
        ("open-source", ("open source", "open-source", "hugging face", "weights")),
        ("business", ("funding", "raises", "acquire", "revenue", "valuation", "ipo")),
        ("robotics", ("robot", "robotics", "humanoid")),
    ]
    for tag, needles in rules:
        if any(n in text for n in needles):
            tags.append(tag)
    return tags[:4]
