from fastapi import FastAPI
from pydantic import BaseModel
import pandas as pd
import joblib
import shap

app = FastAPI()

model = joblib.load("stroke.pkl")

pipeline = model["pipeline"]
background = model["background"]

preprocessor = pipeline.named_steps["preprocessor"]
classifier = pipeline.named_steps["classifier"]

explainer = shap.LinearExplainer(classifier, background)


class StrokeRequest(BaseModel):
    Age: float
    Hypertension: float
    Heart_Disease: float
    BMI: float
    Avg_Glucose: float
    Diabetes: float

    Gender: str
    SES: str
    Smoking_Status: str


def original_feature(name):
    name = name.replace("num__", "")
    name = name.replace("cat__", "")

    if name.startswith("Gender_"):
        return "Gender"

    if name.startswith("SES_"):
        return "SES"

    if name.startswith("Smoking_Status_"):
        return "Smoking_Status"

    return name


@app.post("/predict")
def predict(req: StrokeRequest):

    df = pd.DataFrame(
        [
            {
                "Age": req.Age,
                "Hypertension": req.Hypertension,
                "Heart_Disease": req.Heart_Disease,
                "BMI": req.BMI,
                "Avg_Glucose": req.Avg_Glucose,
                "Diabetes": req.Diabetes,
                "Gender": req.Gender,
                "SES": req.SES,
                "Smoking_Status": req.Smoking_Status,
            }
        ]
    )

    prediction = int(pipeline.predict(df)[0])

    probability = float(pipeline.predict_proba(df)[0][1])

    if prediction == 1:
        X_processed = preprocessor.transform(df)

        shap_values = explainer(X_processed)

        feature_names = preprocessor.get_feature_names_out()

        impact_df = pd.DataFrame(
            {"feature": feature_names, "impact": shap_values.values[0]}
        )

        impact_df["feature"] = impact_df["feature"].apply(original_feature)

        impact_df = impact_df.groupby("feature", as_index=False).agg({"impact": "sum"})

        positive_impacts = impact_df[impact_df["impact"] > 0]

        top2 = positive_impacts.sort_values("impact", ascending=False).head(2)

        risk_factors = [
            {"feature": row["feature"], "impact": round(float(row["impact"]), 4)}
            for _, row in top2.iterrows()
        ]
        return {
            "prediction": prediction,
            "probability": probability,
            "top_factors": risk_factors,
        }

    return {"prediction": prediction, "probability": probability}
