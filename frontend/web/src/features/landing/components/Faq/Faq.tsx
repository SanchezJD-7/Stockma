import shared from '../../landing.module.css'
import { LandingLead, LandingSectionHead, LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'
import styles from './Faq.module.css'

const faqItems = [
  {
    question: '¿Necesito instalar algo?',
    answer: 'No. Entras desde el navegador, en la computadora o en el celular.',
  },
  {
    question: '¿Cómo consigo mi cuenta?',
    answer:
      'Un administrador de tu negocio crea el acceso y te entrega las credenciales. Si quieres probar antes, pides los 8 días de prueba.',
  },
  {
    question: '¿Puedo usar un lector de códigos de barras?',
    answer: 'Sí. Se conecta como un teclado y el producto aparece apenas pasa el código.',
  },
  {
    question: '¿Qué pasa si dos personas editan el mismo lote?',
    answer:
      'Cada cambio queda guardado al instante y el sistema detecta el choque, para que nadie pise el dato del otro.',
  },
  {
    question: '¿Puedo sacar reportes para el contador?',
    answer: 'Estamos trabajando en reportes a tu medida. Todavía no están disponibles.',
  },
  {
    question: '¿Cuánto cuesta?',
    answer: 'Hay planes según lo que necesites. Los primeros 8 días son gratis.',
  },
]

export function Faq() {
  return (
    <section id='faq' className={`${shared.section} ${shared.spaced} ${shared.deep}`}>
      <div className={shared.wrap}>
        <div className={styles.wrap}>
          <LandingSectionHead>
            <LandingSectionTitle>Preguntas frecuentes</LandingSectionTitle>
            <LandingLead>
              Preguntas clave que nos diferencian de otros softwares de inventario y que te ayudan a
              decidir si Stockma es para ti.
            </LandingLead>
          </LandingSectionHead>
          {faqItems.map(({ question, answer }) => (
            <details key={question} className={styles.item}>
              <summary className={styles.summary}>{question}</summary>
              <div className={styles.answerWrapper}>
                <div className={styles.answer}>
                  <MutedText>{answer}</MutedText>
                </div>
              </div>
            </details>
          ))}
        </div>
      </div>
    </section>
  )
}
