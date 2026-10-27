from aipulse_scraper.relevance import MIN_SCORE, score, tags_for


def test_ai_title_scores_high():
    assert score("OpenAI releases new large language model") >= 50


def test_unrelated_scores_low():
    assert score("How I fixed my bike chain", "Tools and lubricant tips") < MIN_SCORE


def test_floor_applies():
    assert score("Nothing to see", floor=40) == 40


def test_score_capped():
    assert score("AI AI LLM GPT OpenAI Anthropic machine learning deep learning") <= 100


def test_tags():
    t = tags_for("GPT-5 agents for computer vision", "an open-source model")
    assert "llm" in t and len(t) <= 4
