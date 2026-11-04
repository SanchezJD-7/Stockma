import shared from '../../landing.module.css'
import { LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Faq.module.css'

const faqItems = [
  {
    question: '¿Cuánto cuesta?',
    answer: 'Hay planes según lo que necesites. Los primeros 8 días son gratis.',
  },
  {
    question: '¿Funciona para mi negocio?',
    answer: 'Sí, Stockma está pensado para negocios con inventario y vencimientos.',
  },
  {
    question: '¿Puedo probarlo antes de pagar?',
    answer: 'Claro. Todos los planes incluyen una prueba gratuita de 8 días para revisar el flujo.',
  },
  {
    question: '¿Se puede usar con varios usuarios?',
    answer:
      'Sí, según el plan elegido puede haber 1 usuario, varios usuarios o acceso por negocio.',
  },
]

export function Faq() {
  return (
    <section id='faq' className={`${shared.section} ${shared.spaced}`}>
      <div className={styles.wrap}>
        <LandingSectionTitle>Preguntas frecuentes</LandingSectionTitle>
        {faqItems.map(({ question, answer }, index) => (
          <details key={question} className={styles.item} open={index === 0}>
            <summary className={styles.summary}>{question}</summary>
            <div className={styles.answer}>
              <MutedText>{answer}</MutedText>
            </div>
          </details>
        ))}
      </div>
    </section>
  )
}
