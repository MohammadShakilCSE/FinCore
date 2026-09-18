# FinCore Architecture

The backend uses four layers: API, Application, Domain, and Infrastructure.
Dependencies point inward toward the Domain layer. The Domain layer has no
project references to other layers.
